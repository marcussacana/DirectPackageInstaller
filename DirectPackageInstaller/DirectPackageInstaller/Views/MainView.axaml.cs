using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using DirectPackageInstaller.Compression;
using DirectPackageInstaller.Host;
using DirectPackageInstaller.IO;
using DirectPackageInstaller.Tasks;
using DirectPackageInstaller.ViewModels;
using LibOrbisPkg.PKG;
using SharpCompress.Archives;
using Path = System.IO.Path;

namespace DirectPackageInstaller.Views
{
    public partial class MainView : UserControl
    {

        public CNLServer CNL = new CNLServer();

        private Stream PKGStream;
        
        private PkgReader PKGParser;
        private Pkg PKG;

        private bool BadHostAlert;
        
        private Source InputType = Source.NONE;

        private string? LastForcedSource = null;

        private List<(string Source, string Name)> BatchList = new List<(string Source, string Name)>();

        private MainWindow Parent;

        private IArchive? CurrentDecompressor = null;

        public MainViewModel? Model => (MainViewModel?)DataContext;
        
        public MainView()
        {
            InitializeComponent();

            PackagesMenu = this.Find<MenuItem>("PackagesMenu");
            IconBox = this.Find<Image>("IconBox");

            tbURL = this.Find<TextBox>("tbURL");
            
            btnInstallAll = this.Find<MenuItem>("btnInstallAll");
            btnProxyDownload = this.Find<MenuItem>("btnProxyDownload");
            btnRestartServer = this.Find<MenuItem>("btnRestartServer");
            btnAllDebirdEnabled = this.Find<MenuItem>("btnAllDebirdEnabled");
            btnSegmentedDownload = this.Find<MenuItem>("btnSegmentedDownload");
            btnCNLService = this.Find<MenuItem>("btnCNLService");
            btnLoad = this.Find<Button>("btnLoad");

            btnInstallAll.Click += BtnInstallAllOnClick;
            btnRestartServer.Click += RestartServer_OnClick;
            btnProxyDownload.Click += BtnProxyDownloadOnClick;
            btnAllDebirdEnabled.Click += BtnAllDebirdEnabledOnClick;
            btnSegmentedDownload.Click += BtnSegmentedDownloadOnClick;
            btnCNLService.Click += BtnCNLServiceOnClick;
            
            btnLoad.Click += BtnLoadOnClick;
            
            CNL.OnLinksReceived = OnLinksReceived;
        }

        private async void BtnLoadOnClick(object? sender, RoutedEventArgs e)
        {
            if (e != null)
            {
                App.Callback(() => BtnLoadOnClick(sender, null));
                return;
            }
            
            PKGStream?.Close();
            PKGStream?.Dispose();

            string ForcedSource = sender is string ? (string)sender : null;

            string SourcePackage = Model.CurrentURL;

            //Check if the BtnLoadOnClicked has manually called with an file path as source
            if (ForcedSource != null && ForcedSource.Length > 2 && (ForcedSource[1] == ':' || ForcedSource[0] == '/'))
                SourcePackage = LastForcedSource = ForcedSource;
            
            //To hide the Packages menu if a different source is loaded
            if (BatchList.All(x => x.Source != SourcePackage))
                BatchList.Clear();

            if (string.IsNullOrWhiteSpace(SourcePackage) && !string.IsNullOrWhiteSpace(LastForcedSource))
                SourcePackage = LastForcedSource;

            //Already Loaded, then Let's Install it!
            if (InputType != Source.NONE) {
                var Success = await Install(SourcePackage, false);

                if (InputType.HasFlag(Source.File) && Success)
                    App.Callback(() => Model.CurrentURL = string.Empty);
                
                return;
            }

            if (string.IsNullOrEmpty(SourcePackage)) {
                var FD = new OpenFileDialog();
                
                FD.Filters = new List<FileDialogFilter>()
                {
                    new FileDialogFilter()
                    {
                        Name = "ALL PKG Files",
                        Extensions = new List<string>() { "pkg" }
                    },
                    new FileDialogFilter()
                    {
                        Name = "ALL Files",
                        Extensions = new List<string>() { "*" }
                    }
                };

                FD.AllowMultiple = true;
                
                var Result = await FD.ShowAsync(Parent);
                
                BatchList.Clear();
                
                if (Result is {Length: > 0})
                {
                    
                    if (Result.Length > 1)
                        BatchList.AddRange(Result.Select(x => (x, Path.GetFileName(x))));
                    
                    ListEntries(Result);
                    
                    App.Callback(() => Model.CurrentURL = Result.First());
                }

                return;
            }

            PKGStream?.Close();

            bool LimitedFHost = false;

            PKG = null;
            PKGParser = null;
            PKGStream = null;

            Installer.EntryName = null;

            GC.Collect();

            if (ForcedSource == null)
            {
                PackagesMenu.IsVisible = BatchList.Any();
                Installer.CurrentFileList = null;
            }

            InputType = Source.NONE;

            if (!Uri.IsWellFormedUriString(SourcePackage, UriKind.Absolute) && !File.Exists(SourcePackage)) {
                await MessageBox.ShowAsync(Parent, "Invalid URL or File Path", "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                tbURL.IsEnabled = false;
                btnLoad.IsEnabled = false;
                await App.DoEvents();
                
                PKGStream = null;

                if (SourcePackage.EndsWith(".json", StringComparison.InvariantCultureIgnoreCase))
                {
                    PKGStream = SplitHelper.OpenRemoteJSON(SourcePackage);
                    InputType = Source.URL | Source.JSON;
                }
                else if (SourcePackage.Length > 2 && (SourcePackage[1] == ':' || SourcePackage[0] == '/'))
                {
                    PKGStream = File.Open(SourcePackage, FileMode.Open, FileAccess.Read, FileShare.Read);
                    InputType = Source.File;
                }
                else
                {
                    InputType = Source.URL;

                    var FHStream = new FileHostStream(SourcePackage);
                    LimitedFHost = FHStream.SingleConnection;

                    PKGStream = FHStream;
                }

                if (LimitedFHost && !BadHostAlert)
                {
                    await MessageBox.ShowAsync(
                        "This Filehosting is limited, Even though it is compatible with DirectPackageInstaller it is not recommended for use, prefer services like alldebrid to download from this server, otherwise you may have connection and/or speed problems.\nDon't expect to compressed files works as expected as well, the DirectPackageInstaller will need download the entire file before can do anything",
                        "Bad File Hosting Service", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    BadHostAlert = true;
                }

                if (LimitedFHost)
                {
                    if (PKGStream is FileHostStream)
                    {
                        ((FileHostStream) PKGStream).TryBypassProxy = true;
                        ((FileHostStream) PKGStream).KeepAlive = true;
                    }

                    var DownTask = Downloader.CreateTask(SourcePackage, PKGStream);

                    while (DownTask.SafeLength == 0)
                        await Task.Delay(100);

                    InputType |= Source.DiskCache;
                    PKGStream = new VirtualStream(DownTask.OpenRead(), 0, DownTask.SafeLength) {ForceAmount = true};
                }

                byte[] Magic = new byte[4];
                await PKGStream.ReadAsync(Magic, 0, Magic.Length);
                PKGStream.Position = 0;

                if (LimitedFHost && Common.DetectCompressionFormat(Magic) != CompressionFormat.None)
                    await MessageBox.ShowAsync(
                        "You're trying open a compressed file from a limited file hosting,\nMaybe the compressed file must be fully downloaded to open it.",
                        "Bad File Hosting Service", MessageBoxButtons.OK, MessageBoxIcon.Stop);

                ArchiveDataInfo? DataInfo = null;
                switch (Common.DetectCompressionFormat(Magic))
                {
                    case CompressionFormat.RAR:
                        InputType |= Source.RAR;
                        await SetStatus(LimitedFHost ? "Downloading... (It may take a while)" : "Decompressing...");
                        DataInfo = await Decompressor.UnrarPKG(PKGStream, SourcePackage, ForcedSource);
                        break;
                    case CompressionFormat.SevenZip:
                        InputType |= Source.SevenZip;
                        await SetStatus(LimitedFHost ? "Downloading... (It may take a while)" : "Decompressing...");
                        DataInfo = await Decompressor.Un7zPKG(PKGStream, SourcePackage, ForcedSource);
                        break;
                }

                if (DataInfo != null)
                {
                    PKGStream = DataInfo?.Buffer ?? throw new Exception();
                    Installer.EntryName = DataInfo?.Filename;
                    Installer.EntrySize = DataInfo?.Length ?? throw new Exception();
                    CurrentDecompressor = DataInfo?.Archive;
                    ListEntries(Installer.CurrentFileList = DataInfo?.PKGList ?? throw new Exception());
                }

                await SetStatus("Reading PKG...");

                var Info = Installer.CurrentPKG = PKGStream.GetPKGInfo() ?? throw new Exception();

                await SetStatus(Info.Description);

                Model.PKGParams.Clear();
                Model.PKGParams = Info.Params;

                IconBox.Source = Info.Icon;

                btnLoad.Content = "Install";
            }
            catch
            {

                IconBox.Source = null;
                Model.PKGParams?.Clear();

                InputType = Source.NONE;
                btnLoad.Content = "Load";

                PackagesMenu.IsVisible = false;

                await SetStatus("Failed to Open the PKG");
            }
            finally
            {
                tbURL.IsEnabled = true;
                btnLoad.IsEnabled = true;
            }

            PKGStream?.Close();
        }
        public async Task OnShown(MainWindow Parent)
        {
            if (Model == null)
                return;

            this.Parent = Parent;

            Parent.Title += $" - v{SelfUpdate.CurrentVersion}";
            
             if (File.Exists(App.SettingsPath))
            {
                App.Config = new Settings();
                var IniReader = new Ini(App.SettingsPath, "Settings");

                App.Config.PS4IP = IniReader.GetValue("PS4IP");
                App.Config.PCIP = IniReader.GetValue("PCIP");
                App.Config.SearchPS4 = IniReader.GetBooleanValue("SearchPS4");
                App.Config.ProxyDownload = IniReader.GetBooleanValue("ProxyDownload");
                App.Config.SegmentedDownload = IniReader.GetBooleanValue("SegmentedDownload");
                App.Config.UseAllDebrid = IniReader.GetBooleanValue("UseAllDebrid");
                App.Config.AllDebridApiKey = IniReader.GetValue("AllDebridApiKey");
                App.Config.EnableCNL = IniReader.GetBooleanValue("EnableCNL");

                var Concurrency = IniReader.GetValue("Concurrency");
                if (!string.IsNullOrWhiteSpace(Concurrency) && int.TryParse(Concurrency, out int ConcurrencyNum))
                    SegmentedStream.DefaultConcurrency = ConcurrencyNum;
                
            }
            else
            {
                App.Config = new Settings()
                {
                    PCIP = "0.0.0.0",
                    PS4IP = "",
                    SearchPS4 = true,
                    ProxyDownload = false,
                    SegmentedDownload = true,
                    UseAllDebrid = false,
                    EnableCNL = true,
                    AllDebridApiKey = null
                };

                await MessageBox.ShowAsync($"Hello User, The focus of this tool is download PKGs from direct links but we have others minor features as well.\n\nGood to know:\nWhen using the direct download mode, you can turn off the computer or close the DirectPakcageInstaller and your PS4 will continue the download alone.\n\nWhen using the \"Proxy Downloads\" feature, the PS4 can't download the game alone and the DirectPackageInstaller must keep open.\n\nDirect PKG urls, using the \"Proxy Download\" feature or not, can be resumed anytime by just selecting 'resume' in your PS4 download list.\n\nThe DirectPackageInstaller use the port {Installer.ServerPort} in the \"Proxy Downloads\" feature, maybe you will need to open the ports in your firewall.\n\nWhen downloading directly from compressed files, you can't resume the download after the DirectPackageInstaller is closed, but before close the DirectPackageInstaller you still can pause and resume the download in your PS4.\n\nIf your download speed is very slow, you can try enable the \"Proxy Downloads\" feature, since this feature has been created just to optimize the download speed.\n\nCreated by marcussacana", "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

             Model.CNLService = App.Config.EnableCNL;
             Model.ProxyMode = App.Config.ProxyDownload;
             Model.UseAllDebird = App.Config.UseAllDebrid;
             Model.SegmentedMode = App.Config.SegmentedDownload;
             Model.PS4IP = App.Config.PS4IP;
             Model.PCIP = App.Config.PCIP;
             Model.AllDebirdApiKey = App.Config.AllDebridApiKey;
             
            if (App.Config.SearchPS4 || string.IsNullOrEmpty(App.Config.PS4IP))
            {
                _ = PS4Finder.StartFinder((PS4IP, PCIP) =>
                {
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (string.IsNullOrEmpty(Model.PS4IP))
                        {
                            Model.PS4IP = PS4IP.ToString();
                            
                            var NewPCIP = PCIP?.ToString() ?? IPHelper.FindLocalIP(PS4IP.ToString()) ?? "";
                            if (!string.IsNullOrWhiteSpace(NewPCIP) && NewPCIP != "0.0.0.0")
                                Model.PCIP = NewPCIP;
                            
                            RestartServer_OnClick(null, null);
                        }
                    });
                });
            }
            
             if (!string.IsNullOrEmpty(App.Config.PS4IP))
                 new Thread(() => Installer.StartServer(App.Config.PCIP)).Start();

             if (App.Config.EnableCNL)
             {
                 try
                 {
                     CNL.Server.Start();
                 }
                 catch
                 {
                     App.Callback(async () =>
                     {
                         await MessageBox.ShowAsync("Failed to start the CNL Server\nMaybe another process is already listening?", "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                     });
                 }
             }
             
             Model.PropertyChanged += ModelOnPropertyChanged;
             
             App.Callback(async () =>
             {
                 if (await App.Updater.HasUpdates())
                 {
                     var Response = await MessageBox.ShowAsync($"New Update Found, You're using the {SelfUpdate.CurrentVersion} the last version is {SelfUpdate.LastVersion},\nDo you wanna update the DirectPackageInstaller now?", "DirectPackageInstaller", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                     if (Response != DialogResult.Yes)
                         return;
                     
                     await App.Updater.DownloadUpdate();
                 }
             });
        }

        private void ModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (Model == null)
                return;
            
            switch (e.PropertyName)
            {
                case "ProxyMode":
                    App.Config.ProxyDownload = Model.ProxyMode;
                    break;
                case "SegmentedMode":
                    App.Config.SegmentedDownload = Model.SegmentedMode;
                    break;
                case "PS4IP":
                    App.Config.PS4IP = Model.PS4IP;
                    break;
                case "PCIP":
                    App.Config.PCIP = Model.PCIP;
                    break;
                case "UseAllDebird":
                    App.Config.UseAllDebrid = Model.UseAllDebird;
                    break;
                case "AllDebirdApiKey":
                    App.Config.AllDebridApiKey = Model.AllDebirdApiKey;
                    break;
                case "CNLService":
                    App.Config.EnableCNL = Model.CNLService;

                    try
                    {
                        if (Model.CNLService && !CNL.Server.IsListening)
                            CNL.Server.Start();
                        else if (!Model.CNLService)
                            CNL.Server.Stop();
                    }
                    catch
                    {
                        if (Model.CNLService)
                        {
                            App.Callback(async () =>
                            {
                                await MessageBox.ShowAsync("Failed to start the CNL Server\nMaybe another process is already listening?", "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            });
                        }
                    }
                    break;
                case "CurrentURL":
                    UrlChanged(Model.CurrentURL);
                    break;
            }
        }

        private void BtnCNLServiceOnClick(object? sender, RoutedEventArgs e)
        {
            if (Model == null)
                return;
            
            Model.CNLService = !Model.CNLService;
        }
        
        private void BtnSegmentedDownloadOnClick(object? sender, RoutedEventArgs e)
        {
            if (Model == null)
                return;
            
            Model.SegmentedMode = !Model.SegmentedMode;
        }

        private void BtnProxyDownloadOnClick(object? sender, RoutedEventArgs e)
        {
            if (Model == null)
                return;
            
            Model.ProxyMode = !Model.ProxyMode;
        }

        private void BtnAllDebirdEnabledOnClick(object? sender, RoutedEventArgs e)
        {
            if (Model == null)
                return;
            
            Model.UseAllDebird = !Model.UseAllDebird;
        }

        private async Task<bool> Install(string URL, bool Silent)
        {
            if (string.IsNullOrWhiteSpace(App.Config.PS4IP) || string.IsNullOrWhiteSpace(App.Config.PCIP))
            {
                await MessageBox.ShowAsync(Parent, "Failed to detect your PS4 IP.\nPlease, Type your PS4/PC IP in the options menu", "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            
            btnLoad.Content = "Pushing...";
            PackagesMenu.IsEnabled = false;
            btnLoad.IsEnabled = false;
            tbURL.IsEnabled = false;
            try
            {
                if (!IPHelper.IsRPIOnline(App.Config.PS4IP) && !await Installer.TryConnectSocket(App.Config.PS4IP))
                {
                    await MessageBox.ShowAsync($"Remote Package Installer Not Found at {App.Config.PS4IP}, Ensure if he is open.", "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                return await Installer.PushPackage(App.Config, InputType, PKGStream, URL, SetStatus, () => Status.Text, Silent);
            }
            catch
            {
                return false;
            }
            finally {
                PackagesMenu.IsEnabled = true;
                btnLoad.IsEnabled = true;
                tbURL.IsEnabled = true;
                btnLoad.Content = "Install";
            }
        }
        
        private async void BtnInstallAllOnClick(object? sender, RoutedEventArgs? e)
        {
            if (e != null)
            {
                App.Callback(() => BtnInstallAllOnClick(sender, null));
                return;
            }

            if (!BatchList.Any() && (Installer.CurrentFileList == null || CurrentDecompressor == null))
                return;

            foreach (var File in Installer.CurrentFileList ?? BatchList.Select(x => x.Source))
            {
                var ContentSource = tbURL.Text;
                
                if (string.IsNullOrWhiteSpace(ContentSource))
                    ContentSource = File;
                else
                    Installer.EntryName = File;

                if (InputType.HasFlag(Source.SevenZip) || InputType.HasFlag(Source.RAR))
                {
                    Stream? Stream = null;
                    
                    try
                    {
                        var Entry = CurrentDecompressor.Entries.Single(x =>
                            x.Key.EndsWith(File, StringComparison.InvariantCultureIgnoreCase));
                        Stream = Entry.OpenEntryStream();

                        if (!Stream.CanSeek)
                            Stream = new ReadSeekableStream(Stream, TempHelper.GetTempFile(null));

                        Installer.CurrentPKG = Stream.GetPKGInfo() ?? throw new Exception();
                    }
                    catch
                    {
                        continue;
                    }
                    finally
                    {
                        Stream?.Close();
                    }
                } 
                else if (InputType.HasFlag(Source.File))
                {
                    ContentSource = File;
                    
                    try
                    {
                         using (FileStream Stream = new FileStream(File, FileMode.Open))
                            Installer.CurrentPKG = Stream.GetPKGInfo() ?? throw new Exception();
                    } catch { continue; }
                }

                if (!await Install(ContentSource, true)) {
                    var Reply = await MessageBox.ShowAsync("Continue trying install the others packages?", "DirectPackageInstaller", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (Reply != DialogResult.Yes)
                        break;
                }

                if (Installer.Server != null)
                {
                    while (Installer.Server.Decompress.Tasks.Any(x => x.Value.Running))
                        await Task.Delay(5000);
                }
            }

            await MessageBox.ShowAsync("Packages Sent!", "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ListEntries(string[]? PKGList)
        {
            if (PKGList == null)
                return;
            
            PackagesMenu.IsVisible = PKGList.Length > 1;
            
            if (PackagesMenu.IsVisible)
            {
                List<TemplatedControl> ToRemove = new List<TemplatedControl>();
                List<TemplatedControl> Items = PackagesMenu.Items.Cast<TemplatedControl>().ToList();
                
                foreach (var Item in Items)
                {
                    if (Item is Separator)
                        break;
                    
                    ToRemove.Add(Item);
                }

                foreach (var Item in ToRemove)
                {
                    Items.Remove(Item);
                }

                foreach (var Entry in PKGList.OrderByDescending(x=>x))
                {
                    var Item = new MenuItem()
                    {
                        Header = Path.GetFileName(Entry)
                        
                    };
                    
                    Item.Click += (sender, e) =>
                    {
                        InputType = Source.NONE;
                        App.Callback(() => BtnLoadOnClick(Entry, null));
                    };

                    Items.Insert(0, Item);
                }

                PackagesMenu.Items = Items;
            }
        }
        

        private void OnLinksReceived((string[] Links, string Password) Info)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                App.Callback(() => OnLinksReceived(Info));
                return;
            }
            
            _ = URLAnalyzer.Analyze(Info.Links);

            tbURL.Text = Info.Links.First();
            App.Callback(() => BtnLoadOnClick(null, new RoutedEventArgs()));
        }
        
        private void UrlChanged(string Url)
        {
            InputType = Source.NONE;

            btnLoad.Content = (string.IsNullOrWhiteSpace(Url) && !File.Exists(Url)) ? "Open" : "Load";
            
            Installer.CurrentFileList = null;
            LastForcedSource = null;

            PackagesMenu.IsVisible = BatchList.Any();

            if (Url.Contains("\n"))
            {
                var Links = Url.Replace("\r\n", "\n").Split('\n')
                    .Where(x => x.Trim().StartsWith("http", StringComparison.InvariantCultureIgnoreCase))
                    .Select(x => x.Trim()).ToArray();
                
                if (!Links.Any())
                {
                    App.Callback(() => Model!.CurrentURL = "");
                    return;
                }

                _ = URLAnalyzer.Analyze(Links.Select(x =>x.Trim()).ToArray());
                
                App.Callback(() => Model!.CurrentURL = Links.First());
                return;
            }

            if (Url.StartsWith("http", StringComparison.InvariantCultureIgnoreCase) && !Uri.IsWellFormedUriString(Url, UriKind.Absolute)) {
                int PathOrQueryPos = Url.IndexOfAny(new char[] { '/', '?' }, Url.IndexOf("://", StringComparison.Ordinal) + 3);
                var Host = Url.Substring(0, PathOrQueryPos);

                var PathAndQuery = Url.Substring(PathOrQueryPos);


                var PathOnly = Uri.UnescapeDataString(PathAndQuery.Split('?').First());
                var QueryOnly = PathAndQuery.Contains('?') ? PathAndQuery.Substring(PathAndQuery.IndexOf('?')) : "";

                PathOnly = Uri.EscapeDataString(PathOnly);

                Dictionary<string, string> PathReplaces = new Dictionary<string, string>()
                { 
                    { "%2f", "/" }, { "%2F", "/" }, { "[", "%5b" }, { "]", "%5d" }, { "%2b", "+" }, { "%2B", "+" },
                    { "%28", "(" }, { "%29", ")" } 
                };

                Dictionary<string, string> QueryReplaces = new Dictionary<string, string>()
                {
                    { "%2f", "/" }, { "%2F", "/" }, { "[", "%5b" }, { "]", "%5d" }
                };

                foreach (var Pair in PathReplaces)
                    PathOnly = PathOnly.Replace(Pair.Key, Pair.Value);

                foreach (var Pair in QueryReplaces)
                    QueryOnly = QueryOnly.Replace(Pair.Key, Pair.Value);

                var NewUrl = $"{Host}{PathOnly}{QueryOnly}";
                if (Uri.IsWellFormedUriString(NewUrl, UriKind.Absolute))
                    Model!.CurrentURL = NewUrl;
            }

            PKGStream?.Close();
            PKGStream?.Dispose();
        }


        private async Task SetStatus(string Status) {
            
            if (!Dispatcher.UIThread.CheckAccess())
            {
                await Dispatcher.UIThread.InvokeAsync(async () => await SetStatus(Status));
                return;
            }

            try
            {
                this.Status.Text = Status;
                await App.DoEvents();
            }
            catch
            {
                App.Callback(() => this.Status.Text = Status);
            }
        }
        
        private void RestartServer_OnClick(object? sender, RoutedEventArgs? e)
        {
            if (Model == null)
                return;
            
            try
            {
                Installer.Server?.Stop();
            }
            catch { }

            PS4Server pS4Server = new PS4Server(Model.PCIP);
            Installer.Server = pS4Server;
            Installer.Server.Start();
        }
    }
}