using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using DirectPackageInstaller.Compression;
using DirectPackageInstaller.Host;
using DirectPackageInstaller.IO;
using DirectPackageInstaller.Tasks;
using DirectPackageInstaller.ViewModels;
using LibOrbisPkg.PKG;
using LibOrbisPkg.SFO;
using Path = System.IO.Path;

namespace DirectPackageInstaller.Views
{
    public partial class MainView : UserControl
    {
        
        private Stream PKGStream;
        
        private PkgReader PKGParser;
        private Pkg PKG;

        private bool Fake;
        private bool BadHostAlert;
        
        private Source InputType = Source.NONE;

        private string LastForcedSource = null;

        private MainWindow Parent = null;
        
        
        
        public MainViewModel? Model => (MainViewModel?)DataContext;
        
        public MainView()
        {
            InitializeComponent();

            PackagesMenu = this.Find<MenuItem>("PackagesMenu");
            IconBox = this.Find<Image>("IconBox");

            tbURL = this.Find<TextBox>("tbURL");
            
            btnProxyDownload = this.Find<MenuItem>("btnProxyDownload");
            btnRestartServer = this.Find<MenuItem>("btnRestartServer");
            btnAllDebirdEnabled = this.Find<MenuItem>("btnAllDebirdEnabled");
            btnSegmentedDownload = this.Find<MenuItem>("btnSegmentedDownload");
            btnLoad = this.Find<Button>("btnLoad");

            btnRestartServer.Click += RestartServer_OnClick;
            btnProxyDownload.Click += BtnProxyDownloadOnClick;
            btnAllDebirdEnabled.Click += BtnAllDebirdEnabledOnClick;
            btnSegmentedDownload.Click += BtnSegmentedDownloadOnClick;
            
            btnLoad.Click += BtnLoadOnClick;
        }

        private async void BtnLoadOnClick(object? sender, RoutedEventArgs e)
        {
            PKGStream?.Close();
            PKGStream?.Dispose();

            string ForcedSource = sender is string ? (string)sender : null;

            string SourcePackage = Model.CurrentURL;

            if (ForcedSource != null && ForcedSource.Length > 2 && (ForcedSource[1] == ':' || ForcedSource[0] == '/'))
                SourcePackage = LastForcedSource = ForcedSource;

            if (string.IsNullOrWhiteSpace(SourcePackage) && !string.IsNullOrWhiteSpace(LastForcedSource))
                SourcePackage = LastForcedSource;

            if (InputType != Source.NONE) {
                var Success = await Install(SourcePackage, false);

                if (InputType.HasFlag(Source.File) && Success)
                    Model.CurrentURL = string.Empty;

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
                
                var Result = await FD.ShowAsync(Parent);
                if (Result != null && Result.Length > 0)
                    Model.CurrentURL = Result.First();
                
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
                PackagesMenu.IsVisible = false;
                Installer.CurrentFileList = null;
            }

            InputType = Source.NONE;

            if (!Uri.IsWellFormedUriString(SourcePackage, UriKind.Absolute) && !File.Exists(SourcePackage)) {
                await MessageBox.ShowAsync(Parent, "Invalid URL or File Path", "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);

                return;
            }
            try
            {
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
                    await MessageBox.ShowAsync("This Filehosting is limited, Even though it is compatible with DirectPackageInstaller it is not recommended for use, prefer services like alldebrid to download from this server, otherwise you may have connection and/or speed problems.\nDon't expect to compressed files works as expected as well, the DirectPackageInstaller will need download the entire file before can do anything", "Bad File Hosting Service", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    BadHostAlert = true;
                }

                if (LimitedFHost)
                {
                    if (PKGStream is FileHostStream)
                    {
                        ((FileHostStream)PKGStream).TryBypassProxy = true;
                        ((FileHostStream)PKGStream).KeepAlive = true;
                    }

                    var DownTask = Downloader.CreateTask(SourcePackage, PKGStream);
                    
                    while (DownTask.SafeLength == 0)
                        await Task.Delay(100);

                    InputType |= Source.DiskCache;
                    PKGStream = new VirtualStream(DownTask.OpenRead(), 0, DownTask.SafeLength) { ForceAmount = true };
                }

                byte[] Magic = new byte[4];
                PKGStream.Read(Magic, 0, Magic.Length);
                PKGStream.Position = 0;

                if (LimitedFHost && Common.DetectCompressionFormat(Magic) != CompressionFormat.None)
                    await MessageBox.ShowAsync("You're trying open a compressed file from a limited file hosting,\nMaybe the compressed file must be fully downloaded to open it.", "Bad File Hosting Service", MessageBoxButtons.OK, MessageBoxIcon.Stop);

                switch (Common.DetectCompressionFormat(Magic))
                {
                    case CompressionFormat.RAR:
                        InputType |= Source.RAR;
                        SetStatus(LimitedFHost ? "Downloading... (It may take a while)" : "Decompressing...");
                        var Unrar = Decompressor.UnrarPKG(PKGStream, SourcePackage, ForcedSource);
                        PKGStream = Unrar.Buffer;
                        Installer.EntryName = Unrar.Filename;
                        Installer.EntrySize = Unrar.Length;
                        ListEntries(Installer.CurrentFileList = Unrar.PKGList);
                        break;

                    case CompressionFormat.SevenZip:
                        InputType |= Source.SevenZip;
                        SetStatus(LimitedFHost ? "Downloading... (It may take a while)" : "Decompressing...");
                        var Un7z = Decompressor.Un7zPKG(PKGStream, SourcePackage, ForcedSource);
                        PKGStream = Un7z.Buffer;
                        Installer.EntryName = Un7z.Filename;
                        Installer.EntrySize = Un7z.Length;
                        ListEntries(Installer.CurrentFileList = Un7z.PKGList);
                        break;
                }

                SetStatus("Reading PKG...");

                PKGParser = new PkgReader(PKGStream);
                PKG = PKGParser.ReadPkg();

                Installer.PKGEntries = PKG.Metas.Metas.Select(x => (x.DataOffset, x.DataOffset + x.DataSize, x.DataSize, x.id)).ToArray();

                var SystemVer = PKG.ParamSfo.ParamSfo.HasName("SYSTEM_VER") ? PKG.ParamSfo.ParamSfo["SYSTEM_VER"].ToByteArray() : new byte[4];
                var TitleName = Encoding.UTF8.GetString(PKG.ParamSfo.ParamSfo.HasName("TITLE") ? PKG.ParamSfo.ParamSfo["TITLE"].ToByteArray() : new byte[0]).Trim('\x0');

                Fake = PKG.CheckPasscode("00000000000000000000000000000000");
                SetStatus($"[{SystemVer[3]:X2}.{SystemVer[2]:X2} - {(Fake ? "Fake" : "Retail")}] {TitleName}");

                Model.PKGParams.Clear();
                IconBox.Source = null;

                try
                {

                    List<PkgParamInfo> Params = new List<PkgParamInfo>();
                    
                    foreach (var Param in PKG.ParamSfo.ParamSfo.Values)
                    {
                        var Name = Param.Name;
                        var RawValue = Param.ToByteArray();

                        bool DecimalValue = new[] { "APP_TYPE", "PARENTAL_LEVEL", "DEV_FLAG" }.Contains(Name);

                        var Value = Param.Type switch
                        {
                            SfoEntryType.Utf8Special => "",
                            SfoEntryType.Utf8 => Encoding.UTF8.GetString(RawValue).Trim('\x0'),
                            SfoEntryType.Integer => BitConverter.ToUInt32(RawValue, 0).ToString(DecimalValue ? "D1" : "X8"),
                            _ => throw new NotImplementedException(),
                        };

                        if (Name == "CATEGORY")
                            Value = ParseCategory(Value);

                        if (string.IsNullOrWhiteSpace(Value))   
                            continue;

                        Params.Add(new PkgParamInfo() {Name = Name, Value = Value});
                    }

                    Model.PKGParams = new List<PkgParamInfo>(Params);
                }
                catch { }

                if (PKG.Metas.Metas.Where(entry => entry.id == EntryId.ICON0_PNG).FirstOrDefault() is MetaEntry Icon)
                {
                    try
                    {
                        PKGStream.Position = Icon.DataOffset;
                        byte[] Buffer = new byte[Icon.DataSize];
                        PKGStream.Read(Buffer, 0, Buffer.Length);

                        using (Stream ImgBuffer = new MemoryStream(Buffer))
                        {
                            var IconBitmap = Bitmap.DecodeToHeight(ImgBuffer, 512);
                            IconBox.Source = IconBitmap;
                            
                        }
                    }
                    catch { }
                }

                btnLoad.Content = "Install";
            }
            catch {
                
                IconBox.Source = null;
                Model.PKGParams?.Clear();

                InputType = Source.NONE;
                btnLoad.Content = "Load";

                PackagesMenu.IsVisible = false;

                SetStatus("Failed to Open the PKG");
            }

            PKGStream?.Close();
        }

        public async Task OnShown(MainWindow Parent)
        {
            if (Model == null)
                return;

            this.Parent = Parent;
            
            if (App.Updater.HaveUpdate() && await MessageBox.ShowAsync(Parent, "Update found, Update now?", "DirectPackageInstaller", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                App.Updater.Update();
                return;
            }
            
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
                    AllDebridApiKey = null
                };

                await MessageBox.ShowAsync($"Hello User, The focus of this tool is download PKGs from direct links but we have others minor features as well.\n\nGood to know:\nWhen using the direct download mode, you can turn off the computer or close the DirectPakcageInstaller and your PS4 will continue the download alone.\n\nWhen using the \"Proxy Downloads\" feature, the PS4 can't download the game alone and the DirectPackageInstaller must keep open.\n\nDirect PKG urls, using the \"Proxy Download\" feature or not, can be resumed anytime by just selecting 'resume' in your PS4 download list.\n\nThe DirectPackageInstaller use the port {Installer.ServerPort} in the \"Proxy Downloads\" feature, maybe you will need to open the ports in your firewall.\n\nWhen downloading directly from compressed files, you can't resume the download after the DirectPackageInstaller is closed, but before close the DirectPackageInstaller you still can pause and resume the download in your PS4.\n\nIf your download speed is very slow, you can try enable the \"Proxy Downloads\" feature, since this feature has been created just to optimize the download speed.\n\nCreated by marcussacana", "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

             Model.ProxyMode = App.Config.ProxyDownload;
             Model.UseAllDebird = App.Config.UseAllDebrid;
             Model.SegmentedMode = App.Config.SegmentedDownload;
             Model.PS4IP = App.Config.PS4IP;
             Model.PCIP = App.Config.PCIP;
             Model.AllDebirdApiKey = App.Config.AllDebridApiKey;
             
             
             if (App.Config.SearchPS4 || string.IsNullOrEmpty(App.Config.PS4IP))
             {
                 Locator.OnPS4DeviceFound = (IP) =>
                 {
                     Dispatcher.UIThread.InvokeAsync(() =>
                     {
                         if (string.IsNullOrEmpty(Model.PS4IP))
                         {
                             Model.PS4IP = IP;
                             Model.PCIP = Locator.FindLocalIP(IP);
                             RestartServer_OnClick(null, null);
                         }
                     });
                 };
             }

             if (string.IsNullOrEmpty(App.Config.PS4IP) || !Locator.IsValidPS4IP(Model.PS4IP))
                 new Thread(() => Locator.Locate(string.IsNullOrEmpty(App.Config.PS4IP))).Start();

             if (!string.IsNullOrEmpty(App.Config.PS4IP))
                 new Thread(() => Installer.StartServer(App.Config.PCIP)).Start();
             
             Model.PropertyChanged += ModelOnPropertyChanged;
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
                case "CurrentURL":
                    UrlChanged(Model.CurrentURL);
                    break;
            }
        }
        
        void UrlChanged(string Url)
        {
            InputType = Source.NONE;
            
            btnLoad.Content = (string.IsNullOrWhiteSpace(Url) || File.Exists(Url)) ? "Open" : "Load";
            Installer.CurrentFileList = null;
            LastForcedSource = null;

            PackagesMenu.IsVisible = false;

            if (Url.Contains("\n"))
            {
                var Links = Url.Replace("\r\n", "\n").Split('\n')
                    .Where(x => x.StartsWith("http", StringComparison.InvariantCultureIgnoreCase)).ToArray();

                Url = Links.First();
                Decompressor.CompressInfo[Url.Trim()] = (Links.Select(x =>x.Trim()).ToArray(), null);
                
                new Task(async() =>
                {
                    var Link = Links.First();
                    await Task.Delay(20);
                    await Dispatcher.UIThread.InvokeAsync(() => Model.CurrentURL = Link);
                }).Start();
                return;
            }

            if (Url.StartsWith("http", StringComparison.InvariantCultureIgnoreCase) && !Uri.IsWellFormedUriString(Url, UriKind.Absolute)) {
                int PathOrQueryPos = Url.IndexOfAny(new char[] { '/', '?' }, Url.IndexOf("://") + 3);
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
                    Model.CurrentURL = NewUrl;
            }

            PKGStream?.Close();
            PKGStream?.Dispose();
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
            btnLoad.Content = "Pushing...";
            PackagesMenu.IsEnabled = false;
            btnLoad.IsEnabled = false;
            tbURL.IsEnabled = false;
            try
            {
                if (!Locator.IsValidPS4IP(App.Config.PS4IP))
                {
                    await MessageBox.ShowAsync($"Remote Package Installer Not Found at {App.Config.PS4IP}, Ensure if he is open.", "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                };
                
                return await Installer.PushPackage(App.Config, InputType, PKGStream, URL, SetStatus, () => Status.Text, Silent);
            }
            finally {
                PackagesMenu.IsEnabled = true;
                btnLoad.IsEnabled = true;
                tbURL.IsEnabled = true;
                btnLoad.Content = "Install";
            }
        }        

        private string ParseCategory(string CatID) {
            return CatID.ToLowerInvariant().Trim() switch {
                "ac" => "Additional Content",
                "bd" => "Blu-ray Disc",
                "gc" => "Game Content",
                "gd" => "Game Digital Application",
                "gda" => "System Application",
                "gdc" => "Non-Game Big Application",
                "gdd" => "BG Application",
                "gde" => "Non-Game Mini App / Video Service Native App",
                "gdk" => "Video Service Web App",
                "gdl" => "PS Cloud Beta App",
                "gdo" => "PS2 Classic",
                "gp" => "Game Application Patch",
                "gpc" => "Non-Game Big App Patch",
                "gpd" => "BG Application Patch",
                "gpe" => "Non-Game Mini App Patch / Video Service Native App Patch",
                "gpk" => "Video Service Web App Patch",
                "gpl" => "PS Cloud Beta App Patch",
                "sd" => "Save Data",
                _ => "???"
            } + $" ({CatID})";
        }

        private void ListEntries(string[] PKGList)
        {
            if (PKGList == null)
                return;

            if (PackagesMenu.IsVisible = PKGList.Length > 1)
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
                        BtnLoadOnClick(Entry, null);
                    };

                    Items.Insert(0, Item);
                }

                PackagesMenu.Items = Items;
            }
        }

        private void SetStatus(string Status) {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.InvokeAsync(() => SetStatus(Status));
                return;
            }
            
            this.Status.Text = Status;
            
            App.DoEvents();
        }
        private void RestartServer_OnClick(object? sender, RoutedEventArgs e)
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