using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LibOrbisPkg.PKG;
using SharpCompress.Archives;
using DirectPackageInstaller.Compression;
using DirectPackageInstaller.Host;
using DirectPackageInstaller.IO;
using DirectPackageInstaller.Others;
using DirectPackageInstaller.Tasks;
using DirectPackageInstaller.ViewModels;
using Image = Avalonia.Controls.Image;
using Path = System.IO.Path;
using Size = Avalonia.Size;
using LibOrbisPkg.GP4;
using Acornima.Ast;

namespace DirectPackageInstaller.Views
{
    public partial class MainView : UserControl
    {

        public CNLServer CNL = new CNLServer();
        public DHCPHost? DHCP = null;
        public INetworkManagement? AdapterHelper = null;

        private Stream? PKGStream;
        
        private PkgReader PKGParser;
        private Pkg PKG;

        private bool BadHostAlert;
        
        private Source InputType = Source.NONE;

        private string? LastForcedSource = null;

        private List<(string Source, string Name)> BatchList = new List<(string Source, string Name)>();

        private MainWindow Parent;

        private IArchive? CurrentDecompressor = null;

        private DecompressorHelperStream[]? CurrentDecompressorVolumes = null;

        private DateTime LastTransferProgressUpdate = DateTime.MinValue;

        public MainViewModel? Model => (MainViewModel?)DataContext;
        
        public MainView()
        {
            InitializeComponent();

            MainViewGrid = this.Find<Grid>("MainViewGrid");

            PackagesMenu = this.Find<MenuItem>("PackagesMenu");

            tbURL = this.Find<TextBox>("tbURL");

            PreviewGrid = this.Find<Grid>("PreviewGrid");
            
            IconBox = this.Find<Image>("IconBox");
            MainGridSplitter = this.Find<GridSplitter>("MainGridSplitter");
            PkgInfoGrid = this.Find<DataGrid>("PkgInfoGrid");
            
            btnInstallAll = this.Find<MenuItem>("btnInstallAll");
            btnProxyDownload = this.Find<MenuItem>("btnProxyDownload");
            btnRestartServer = this.Find<MenuItem>("btnRestartServer");
            btnAllDebirdEnabled = this.Find<MenuItem>("btnAllDebirdEnabled");
            btnRealDebirdEnabled = this.Find<MenuItem>("btnRealDebirdEnabled");
            btnDebirdLinkEnabled = this.Find<MenuItem>("btnDebirdLinkEnabled");
            btnSegmentedDownload = this.Find<MenuItem>("btnSegmentedDownload");
            btnTransferProgress = this.Find<MenuItem>("btnTransferProgress");
            btnErrorLog = this.Find<MenuItem>("btnErrorLog");
            btnCNLService = this.Find<MenuItem>("btnCNLService");
            btnDHCPService = this.Find<MenuItem>("btnDHCPService");
            btnExit = this.Find<MenuItem>("btnExit");
            btnLoad = this.Find<Button>("btnLoad");
            
            btnInstallAll.Click += BtnInstallAllOnClick;
            btnRestartServer.Click += RestartServer_OnClick;
            btnProxyDownload.Click += BtnProxyDownloadOnClick;
            btnAllDebirdEnabled.Click += BtnAllDebirdEnabledOnClick;
            btnRealDebirdEnabled.Click += BtnRealDebirdEnabledOnClick;
            btnDebirdLinkEnabled.Click += BtnDebirdLinkEnabledOnClick;
            btnSegmentedDownload.Click += BtnSegmentedDownloadOnClick;
            btnTransferProgress.Click += BtnTransferProgressOnClick;
            btnErrorLog.Click += BtnErrorLogOnClick;
            btnCNLService.Click += BtnCNLServiceOnClick;
            btnDHCPService.Click += BtnDHCPServiceOnClick;

            btnLoad.Click += BtnLoadOnClick;

            tbURL.GotFocus += tbURLOnGotFocus;

            btnExit.Click += btnExitOnClick;
            btnExit.IsVisible = App.IsAndroid;
            
            CNL.OnLinksReceived = OnLinksReceived;
            PS4Server.GlobalTransferProgressChanged += ServerOnTransferProgressChanged;
            
            PreviewGrid.PropertyChanged += PreviewGridOnPropertyChanged;

            AddHandler(DragDrop.DragOverEvent, DragOver);
            AddHandler(DragDrop.DropEvent, DropEventEvent);

            btnDHCPService.IsVisible = App.IsWindows;

            Language.Loaded += OnLanguageLoaded;

            btnLanguage.SubmenuOpened += BtnLanguageSubmenuOpened;

            ApplyLanguage();
        }

        /// <summary>
        /// Replaces the language keys set as design time placeholders in the XAML by the
        /// text of the active language, and rebuilds the language sub-menu.
        /// </summary>
        private void ApplyLanguage()
        {
            btnOptions.Header = Language.L("Menu.Options");
            btnProxyDownload.Header = Language.L("Menu.ProxyDownloads");
            btnSegmentedDownload.Header = Language.L("Menu.SegmentedDownloads");
            btnTransferProgress.Header = Language.L("Menu.TransferProgress");
            btnErrorLog.Header = Language.L("Menu.ErrorLog");
            btnCNLService.Header = Language.L("Menu.CNLSupport");
            btnDHCPService.Header = Language.L("Menu.DHCPServer");
            btnIPAddress.Header = Language.L("Menu.IPAddress");
            btnDebrids.Header = Language.L("Menu.Debrids");
            btnAllDebrid.Header = Language.L("Menu.AllDebrid");
            btnRealDebrid.Header = Language.L("Menu.RealDebrid");
            btnDebridLink.Header = Language.L("Menu.DebridLink");
            btnAllDebirdEnabled.Header = Language.L("Menu.Enabled");
            btnRealDebirdEnabled.Header = Language.L("Menu.Enabled");
            btnDebirdLinkEnabled.Header = Language.L("Menu.Enabled");
            btnLanguage.Header = Language.L("Menu.Language");
            btnRestartServer.Header = Language.L("Menu.RestartServer");
            btnExit.Header = Language.L("Menu.Exit");
            PackagesMenu.Header = Language.L("Menu.Packages");
            btnInstallAll.Header = Language.L("Menu.InstallAll");

            lblPSIP.Content = Language.L("Menu.PSIP");
            lblPCIP.Content = Language.L("Menu.PCIP");
            lblAllDebridAPIKey.Content = Language.L("Menu.APIKey");
            lblRealDebridAPIKey.Content = Language.L("Menu.APIKey");
            lblDebridLinkAPIKey.Text = Language.L("Menu.APIKey");
            lblURL.Content = Language.L("Main.URL");

            PkgInfoGrid.Columns[0].Header = Language.L("Main.Param");
            PkgInfoGrid.Columns[1].Header = Language.L("Main.Value");

            AddLanguageMenu();
        }

        private string? LanguageMenuSignature;

        /// <summary>
        /// Rebuilds the language sub menu out of the *.ini files currently sitting in
        /// the language folder. It is also wired to the sub menu opening, so dropping a
        /// new pack in while the app runs is enough to get it listed, no restart needed.
        /// The rebuild is skipped when the folder content didn't change, that way the
        /// sub menu doesn't flicker while it's open.
        /// </summary>
        private void AddLanguageMenu(bool Force = false)
        {
            var Available = Language.Labeled();

            string Signature = string.Join('|', Available.Select(x => x.Key));

            if (!Force && Signature == LanguageMenuSignature)
                return;

            LanguageMenuSignature = Signature;

            btnLanguage.Items.Clear();

            foreach (var Entry in Available)
            {
                var Item = new MenuItem()
                {
                    Header = Entry.Key,
                    Tag = Entry.Value
                };

                if (Entry.Value.Name.Equals(Language.Current, StringComparison.OrdinalIgnoreCase))
                    Item.Classes.Add("SelectedLanguage");

                Item.Click += BtnLanguageOnClick;

                btnLanguage.Items.Add(Item);
            }

            btnLanguage.IsVisible = btnLanguage.Items.Count > 0;
        }

        private void BtnLanguageSubmenuOpened(object? sender, EventArgs e) => AddLanguageMenu();

        private void BtnLanguageOnClick(object? sender, RoutedEventArgs e)
        {
            var Selected = (sender as MenuItem)?.Tag as LanguageInfo;
            if (Selected == null)
                return;

            if (Selected.Name.Equals(Language.Current, StringComparison.OrdinalIgnoreCase))
                return;

            App.Config.Language = Language.Load(Selected.Name);

            App.SaveSettings();
        }

        private void OnLanguageLoaded(object? sender, EventArgs e)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => OnLanguageLoaded(sender, e));
                return;
            }

            ApplyLanguage();

            AddLanguageMenu(true);

            switch (btnLoad.Tag as string)
            {
                case "Install":
                    btnLoad.Content = Language.L("Main.Install");
                    break;
                case "Load":
                    btnLoad.Content = Language.L("Main.Load");
                    break;
                case "Pushing":
                    btnLoad.Content = Language.L("Main.Pushing");
                    break;
                default:
                    btnLoad.Content = Language.L("Main.Open");
                    break;
            }
        }

        /// <summary>
        /// Keeps the load button caption and its state in sync, the state is stored in
        /// the Tag so the caption can be rebuilt in any language.
        /// </summary>
        private void SetLoadButtonState(string State)
        {
            btnLoad.Tag = State;

            btnLoad.Content = State switch
            {
                "Install" => Language.L("Main.Install"),
                "Load" => Language.L("Main.Load"),
                "Pushing" => Language.L("Main.Pushing"),
                _ => Language.L("Main.Open")
            };
        }

        /// <summary>
        /// First run only, there is no Settings.ini yet so nothing tells us which
        /// language to use. The picker is shown before the welcome text is built, so
        /// the welcome text itself is already in the picked language. Dismissing the
        /// dialog keeps the default.
        /// </summary>
        private async Task SelectLanguageAsync()
        {
            //Language.Labeled also disambiguates packs sharing a native name.
            var Available = Language.Labeled();

            if (Available.Count <= 1)
                return;

            var Labels = Available.Select(x => x.Key).ToArray();
            var Entries = Available.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

            string? Selected = null;

            if (App.IsSingleView)
            {
                var ChoiceBox = Extensions.CreateInstance<SelectView>(new SelectViewModel());
                ((SelectViewModel)ChoiceBox.DataContext).Caption = Language.L("Main.SelectLanguage");

                ChoiceBox.Initialize(null, Labels, (n) =>
                {
                    Selected = n;
                    SingleView.ReturnView(ChoiceBox);
                });

                await SingleView.CallView(ChoiceBox, true);
            }
            else
            {
                var ChoiceBox = new Select(Labels)
                {
                    Caption = Language.L("Main.SelectLanguage"),
                    Title = Language.L("Title.SelectLanguage")
                };

                if (await ChoiceBox.ShowDialogAsync() == DialogResult.OK)
                    Selected = ChoiceBox.Choice;
            }

            if (string.IsNullOrWhiteSpace(Selected))
                return;

            if (!Entries.TryGetValue(Selected!, out var Choice))
                return;

            App.Config.Language = Language.Load(Choice.Name);

            App.SaveSettings();
        }

        public async Task OnShown(MainWindow? Parent)
        {
            if (Model == null)
                return;

            if (Parent != null)
            {
                this.Parent = Parent;

                this.Parent.Title += $" - v{SelfUpdate.CurrentVersion}";
            }

            if (File.Exists(App.SettingsPath))
            {
                App.Config = new Settings();
                var IniReader = new Ini(App.SettingsPath, "Settings");

                App.Config.Language = IniReader.GetValue("Language");
                App.Config.EthernetAdapter = IniReader.GetValue("EthernetAdapter");
                App.Config.EnableDHCP = IniReader.GetBooleanValue("EnableDHCP");

                App.Config.PSIP = IniReader.GetValue("PS4IP");
                App.Config.PCIP = IniReader.GetValue("PCIP");
                App.Config.SearchPS4 = IniReader.GetBooleanValue("SearchPS4");
                App.Config.ProxyDownload = IniReader.GetBooleanValue("ProxyDownload");
                App.Config.SegmentedDownload = IniReader.GetBooleanValue("SegmentedDownload");
                App.Config.UseAllDebrid = IniReader.GetBooleanValue("UseAllDebrid");
                App.Config.AllDebridApiKey = IniReader.GetValue("AllDebridApiKey");
                App.Config.UseRealDebrid = IniReader.GetBooleanValue("UseRealDebrid");
                App.Config.UseDebridLink = IniReader.GetBooleanValue("UseDebridLink");
                App.Config.RealDebridApiKey = IniReader.GetValue("RealDebridApiKey");
                App.Config.DebridLinkApiKey = IniReader.GetValue("DebridLinkApiKey");
                App.Config.EnableCNL = IniReader.GetBooleanValue("EnableCNL");
                App.Config.ShowError = IniReader.GetBooleanValue("ShowError");
                var ShowTransferProgress = IniReader.GetValue("ShowTransferProgress");
                App.Config.ShowTransferProgress = string.IsNullOrWhiteSpace(ShowTransferProgress) || IniReader.GetBooleanValue("ShowTransferProgress");
                App.Config.SkipUpdateCheck = IniReader.GetBooleanValue("SkipUpdateCheck");
                App.Config.AutoSplitPKG = IniReader.GetBooleanValue("AutoSplitPKG");

                App.Config.PayloadPort = IniReader.GetIntValue("PayloadPort");
                
                ConnectionHelper.AllowReconnect = IniReader.GetBooleanValue("AllowReconnect");

                var Concurrency = IniReader.GetValue("Concurrency");
                if (!string.IsNullOrWhiteSpace(Concurrency) && int.TryParse(Concurrency, out int ConcurrencyNum))
                    SegmentedStream.DefaultConcurrency = ConcurrencyNum;

                App.Config.Language = Language.Load(App.Config.Language);

                if (ConnectionHelper.AllowReconnect && !ConnectionHelper.IsWindowsAdministrator())
                {
                    await MessageBox.ShowAsync(Language.L("Msg.AdminRequired"), "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else
            {
                App.Config = new Settings()
                {
                    PCIP = "0.0.0.0",
                    PSIP = "",
                    SearchPS4 = true,
                    ProxyDownload = true,
                    SegmentedDownload = !App.IsAndroid,
                    UseAllDebrid = false,
                    EnableCNL = true,
                    ShowError = false,
                    ShowTransferProgress = true,
                    SkipUpdateCheck = false,
                    EnableDHCP = false,
                    AllDebridApiKey = null,
                    RealDebridApiKey = null,
                    Language = Language.Current
                };

                await SelectLanguageAsync();

                var Hints = new List<string>()
                {
                    Language.L("Welcome.DirectDownload"),
                    Language.L("Welcome.IndirectDownload"),
                    Language.L("Welcome.Resume"),
                    App.IsAndroid ? Language.L("Welcome.Android") : Language.F("Welcome.Firewall", Installer.ServerPort),
                    Language.L("Welcome.Compressed"),
                    Language.L("Welcome.Proxy")
                };

                if (App.IsWindows)
                    Hints.Add(Language.L("Welcome.DHCP"));

                var WelcomeText = Language.L("Welcome.Intro") + string.Join("\n\n", Hints) + "\n\n" + Language.L("Welcome.Footer");
                
                await MessageBox.ShowAsync(WelcomeText, "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            Model.DHCPService = App.Config.EnableDHCP;
            Model.CNLService = App.Config.EnableCNL;
            Model.ProxyMode = App.Config.ProxyDownload;
            Model.ShowTransferProgress = App.Config.ShowTransferProgress;
            Model.ShowError = App.Config.ShowError;
            Model.UseAllDebrid = App.Config.UseAllDebrid;
            Model.UseDebridLink = App.Config.UseDebridLink;
            Model.SegmentedMode = App.Config.SegmentedDownload;
            Model.UseRealDebrid = App.Config.UseRealDebrid;
            Model.PS4IP = App.Config.PSIP;
            Model.PCIP = App.Config.PCIP;
            Model.AllDebridApiKey = App.Config.AllDebridApiKey;
            Model.RealDebridApiKey = App.Config.RealDebridApiKey;
            Model.DebridLinkApiKey = App.Config.DebridLinkApiKey;
             
            if (App.Config.SearchPS4 || string.IsNullOrEmpty(App.Config.PSIP))
            {
                _ = PS4Finder.StartFinder(OnClientFound());
            }

            if (App.Config.EnableDHCP && !string.IsNullOrWhiteSpace(App.Config.EthernetAdapter))
            {
                await StartDHCP(App.Config.EthernetAdapter);
            }

            if (!string.IsNullOrEmpty(App.Config.PSIP))
                await Installer.StartServer(App.Config.PCIP);

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
                          await MessageBox.ShowAsync(Language.L("Msg.CNLServerStartFail"), "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                      });
                  }
              }
 
             Model.PropertyChanged += ModelOnPropertyChanged;
             
             App.Callback(async () =>
             {
                 var MissingRuntime = await App.Updater.RequiresNewRuntime();
                 if (!App.Config.SkipUpdateCheck && MissingRuntime != null)
                 {
                     var Response = await MessageBox.ShowAsync(Language.F("Msg.MissingRuntime", MissingRuntime), Language.L("Title.MissingRuntime"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                     if (Response == DialogResult.Yes)
                     { 
                         OpenUrl($"https://dotnet.microsoft.com/en-us/download/dotnet/{MissingRuntime}/runtime");
                     }
                     return;
                 }
                 
                 if (!App.Config.SkipUpdateCheck && await App.Updater.HasUpdates())
                 {
                     var Response = await MessageBox.ShowAsync(Language.F("Msg.UpdateAvailable", SelfUpdate.CurrentVersion, SelfUpdate.LastVersion), "DirectPackageInstaller", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                     if (Response != DialogResult.Yes)
                         return;
                     
                     await App.Updater.DownloadUpdate();
                 }
             });

             if (string.IsNullOrWhiteSpace(App.WorkingDirectory))
             {
                  await MessageBox.ShowAsync(Language.L("Msg.WorkingDirectoryFail"), "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                 Environment.Exit(0);
             }
        }

        private Action<IPAddress, IPAddress?, int> OnClientFound()
        {
            return (PS4IP, PCIP, VERSION) =>
            {
                Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    if (!string.IsNullOrEmpty(Model.PS4IP))
                    {
                        if (Model.PS4IP == PS4IP.ToString() || await MessageBox.ShowAsync(Language.F("Msg.NewPSFound", VERSION, PS4IP), "DirectPackageInstaller", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
                            return;
                    }
                    Model.PS4IP = PS4IP.ToString();

                    var NewPCIP = PCIP?.ToString() ?? IPHelper.FindLocalIP(PS4IP.ToString()) ?? "";
                    if (!string.IsNullOrWhiteSpace(NewPCIP) && NewPCIP != "0.0.0.0")
                        Model.PCIP = NewPCIP;

                    RestartServer_OnClick(null, null);

                    if (App.IsAndroid)
                        App.SaveSettings();
                });
            };
        }

        private async Task StartDHCP(string? DefaultAdapter = null)
        {
            if (DHCP?.Active ?? false) 
                return;

            if (AdapterHelper == null)
            {
                if (App.IsWindows)
                {
                    AdapterHelper = new WindowsNetworkManagement();
                }
                else
                {
                    if (Model != null)
                        Model!.DHCPService = false;

                    await MessageBox.ShowAsync(Language.L("Msg.DHCPNotSupported"), Language.L("Title.Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            if (DefaultAdapter == null) {

                await SetStatus(Language.L("Status.AnalyzingAdapters"));

                var Adapters = await AdapterHelper.GetValidAdapters();

                if (Adapters.Count() == 1)
                {
                    AdapterHelper.Adapter = Adapters.Single();
                }
                else
                {
                    if (App.IsSingleView)
                    {
                        var ChoiceBox = Extensions.CreateInstance<SelectView>(new SelectViewModel());
                        ((SelectViewModel)ChoiceBox.DataContext).Caption = Language.L("Main.Adapter");
                        ChoiceBox.Initialize(null, Adapters, (n) =>
                        {
                            AdapterHelper.Adapter = n;
                            SingleView.ReturnView(ChoiceBox);
                        });

                        await SingleView.CallView(ChoiceBox, true);
                    }
                    else
                    {
                        var ChoiceBox = new Select(Adapters);
                        ChoiceBox.Caption = Language.L("Main.Adapter");
                        if (await ChoiceBox.ShowDialogAsync() == DialogResult.OK)
                            AdapterHelper.Adapter = ChoiceBox.Choice;
                    }
                }

            }
            else
            {
                AdapterHelper.Adapter = DefaultAdapter;
            }

            if (AdapterHelper.Adapter == null)
            {
                if (Model != null)
                    Model!.DHCPService = false;

                await MessageBox.ShowAsync(Language.L("Msg.FailedToFindAdapter"), Language.L("Title.Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (DHCP == null)
            {
                DHCP = new DHCPHost();
                DHCP.OnNewClient += OnClientFound();
                DHCP.OnNewClient += async (a, b, c) => {
                    await SetStatus(Language.F("Status.DeviceConnected", a));
                };
            }

            App.Config.EthernetAdapter = AdapterHelper.Adapter;

            await SetStatus(Language.L("Status.InitializingDHCP"));

            if (!await App.SetupStaticNetwork())
            {
                if (Model != null)
                    Model!.DHCPService = false;

                await SetStatus("");

                await MessageBox.ShowAsync(Language.L("Msg.FailedToSetupAdapter"), Language.L("Title.Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            DHCP.StartInBackground();

            await SetStatus("");
        }

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch
            {
                // hack because of this: https://github.com/dotnet/corefx/issues/10361
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    throw;
                }
            }
        }
        private async void BtnDHCPServiceOnClick(object? sender, RoutedEventArgs e)
        {
            if (Model == null)
                return;

            Model.DHCPService = !Model.DHCPService;

            if (!Model.DHCPService)
            {
                if (DHCP != null)
                {
                    await SetStatus(Language.L("Status.RestoringNetwork"));

                    DHCP.Stop();
                    await App.SetupDHCPNetwork();

                    await SetStatus("");
                }

                DHCP?.Dispose();
                DHCP = null;

                App.Config.EthernetAdapter = "";
            }
            else
            {
                if (await MessageBox.ShowAsync(Language.L("Msg.DHCPConfirm"), Language.L("Title.Warning"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    await StartDHCP();
                else
                    Model.DHCPService = false;
            }
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

            var ForcedSource = sender as string;

            var SourcePackage = Model?.CurrentURL;

            //Check if the BtnLoadOnClicked has manually called with an file path as source
            if (ForcedSource is {Length: > 2} && (ForcedSource[1] == ':' || ForcedSource[0] == '/'))
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

            //No Source, Open File Selection Dialog
            if (string.IsNullOrEmpty(SourcePackage))
            {

                string[]? Result;

                if (!App.IsSingleView)
                {
                    var FD = new OpenFileDialog
                    {
                        Filters = new List<FileDialogFilter>()
                        {
                            new()
                            {
                                Name = Language.L("Filter.AllPKG"),
                                Extensions = new List<string>() {"pkg", "PKG", "json", "JSON"},
                            },
                            new()
                            {
                                Name = Language.L("Filter.SplitPKG"),
                                Extensions = new List<string>() {"json", "JSON"}
                            },
                            new()
                            {
                                Name = Language.L("Filter.AllFiles"),
                                Extensions = new List<string>() {"*"}
                            }
                        },
                        AllowMultiple = true
                    };

                    Result = await FD.ShowAsync(Parent);
                }
                else
                {
                    var Picker = new FilePicker();
                    await Picker.OpenDir(App.RootDir);
                    
                    await SingleView.CallView(Picker, false);
                    Result = Picker.SelectedFiles.ToArray();
                }

                
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

            Installer.EntryFileName = null;

            GC.Collect();
            
            //Sender is a stream during compressed file selection
            if (ForcedSource == null && sender is not Stream)
            {
                PackagesMenu.IsVisible = BatchList.Any();
                Installer.CurrentFileList = null;
            }

            InputType = Source.NONE;

            if (!SourcePackage.IsValidURL() && !File.Exists(SourcePackage)) {
                await MessageBox.ShowAsync(Parent, Language.L("Msg.InvalidURL"), "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                tbURL.IsEnabled = false;
                btnLoad.IsEnabled = false;
                await App.DoEvents();

                PKGStream = sender as Stream;

                if (PKGStream is null)
                {
                    if (SourcePackage.EndsWith(".json", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (SourcePackage.IsFilePath())
                        {
                            InputType = Source.File | Source.JSON;
                            PKGStream = SplitHelper.OpenLocalJSON(SourcePackage);
                        }
                        else
                        {
                            PKGStream = SplitHelper.OpenRemoteJSON(SourcePackage);
                            InputType = Source.URL | Source.JSON;
                        }
                    }
                    else if (SourcePackage.IsFilePath())
                    {
                        PKGStream = File.Open(SourcePackage, FileMode.Open, FileAccess.Read, FileShare.Read);
                        InputType = Source.File;
                    }
                    else
                    {
                        InputType = Source.URL;

                        var FHStream = await LoadUrl(SourcePackage);

                        if (FHStream == null)
                            throw new AbortException("Failed to Load the URL");

                        LimitedFHost = FHStream.SingleConnection;
                        PKGStream = FHStream;

                    }
                }

                if (LimitedFHost)
                {
                    if (PKGStream is FileHostStream)
                        ((FileHostStream) PKGStream).KeepAlive = true;
                    
                    var DownTask = Downloader.CreateTask(SourcePackage, PKGStream);

                    while (DownTask.SafeLength == 0 && DownTask.Running)
                        await Task.Delay(100);

                    InputType |= Source.DiskCache;
                    PKGStream = new VirtualStream(DownTask.OpenRead(), 0, DownTask.SafeLength) {ForceAmount = true};
                }

                if (LimitedFHost && !BadHostAlert)
                {
                    await MessageBox.ShowAsync(Language.L("Msg.BadFileHosting"), Language.L("Title.BadFileHosting"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    BadHostAlert = true;
                }

                byte[] Magic = new byte[4];
                await PKGStream.ReadAsync(Magic, 0, Magic.Length);
                PKGStream.Position = 0;

                if (LimitedFHost && Common.DetectCompressionFormat(Magic) != CompressionFormat.None)
                    await MessageBox.ShowAsync(Language.L("Msg.BadFileHostingCompressed"), Language.L("Title.BadFileHosting"), MessageBoxButtons.OK, MessageBoxIcon.Stop);

                ArchiveDataInfo? DataInfo = null;
                switch (Common.DetectCompressionFormat(Magic))
                {
                    case CompressionFormat.RAR:
                        InputType |= Source.RAR;
                        await SetStatus(LimitedFHost ? Language.L("Status.Downloading") : Language.L("Status.Decompressing"));
                        DataInfo = await Decompressor.UnrarPKG(PKGStream, SourcePackage, async (s) => await SetStatus(s),ForcedSource);
                        break;
                    case CompressionFormat.SevenZip:
                        InputType |= Source.SevenZip;
                        await SetStatus(LimitedFHost ? Language.L("Status.Downloading") : Language.L("Status.Decompressing"));
                        DataInfo = await Decompressor.Un7zPKG(PKGStream, SourcePackage,async (s) => await SetStatus(s), ForcedSource);
                        break;
                }

                if (DataInfo != null)
                {
                    if (App.IsAndroid && Model.SegmentedMode)
                    {
                        await MessageBox.ShowAsync(Language.L("Msg.AndroidCompressed"), "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        Model.SegmentedMode = false;
                    }
                    PKGStream = DataInfo?.Buffer ?? throw new Exception();
                    Installer.EntryFileName = Path.GetFileName(DataInfo?.Filename);
                    CurrentDecompressor = DataInfo?.Archive;
                    CurrentDecompressorVolumes = DataInfo?.Volumes;
                    ListEntries(Installer.CurrentFileList = DataInfo?.PKGList ?? throw new AbortException("Failed to list compressed files"));
                }

                await SetStatus(Language.L("Status.ReadingPKG"));

                var Info = Installer.CurrentPKG = PKGStream.GetPKGInfo() ?? throw new AbortException("Failed to read the PKG information");

                await SetStatus(Info.Description);

                Model.PKGParams.Clear();
                Model.PKGParams = Info.Params;

                IconBox.Source = Info.Icon;

                if (!App.IsSingleView)
                {
                    Parent.BringIntoView();
                    Parent.Focus();
                    Parent.Activate();
                }

                SetLoadButtonState("Install");
            }
            catch (Exception ex)
            {
                //The type and the code read the same in every language, only the message
                //behind them comes from the OS. The stack goes to the file, not to the
                //dialog, it was never meant to be read by a person.
                var Reason = PS4Server.Error(ex);

                await File.WriteAllTextAsync(Path.Combine(App.WorkingDirectory, "DPI-ERROR.log"), Reason + Environment.NewLine + ex.StackTrace);

                //An abort is what the user asked for, so it stays quiet unless the error
                //log option is on. Anything else is always reported.
                if (ex is not AbortException || App.Config.ShowError)
                    await MessageBox.ShowAsync(Language.F("Msg.UnexpectedError", Reason), $"DirectPackageInstaller - {Language.L("Title.Error")}", MessageBoxButtons.OK, MessageBoxIcon.Error);

                IconBox.Source = null;
                Model.PKGParams?.Clear();

                InputType = Source.NONE;
                SetLoadButtonState("Load");

                PackagesMenu.IsVisible = false;
#if DEBUG
                throw;
#endif

                await SetStatus(Language.L("Status.FailedOpenPKG"));
            }
            finally
            {
                tbURL.IsEnabled = true;
                btnLoad.IsEnabled = true;
            }

            PKGStream?.Close();
        }

        private async Task<FileHostStream?> LoadUrl(string SourcePackage)
        {
            await SetStatus(Language.L("Status.AnalyzingURLs"));

            var UrlInfo = await URLAnalyzer.Analyze(SourcePackage, false);

            while (!UrlInfo.Ready && !UrlInfo.Failed)
            {
                await SetStatus(Language.F("Status.AnalyzingURLsProgress", UrlInfo.Progress));
                await Task.Delay(100);
            }

            if (UrlInfo.Failed)
            {
                URLAnalyzer.URLInfos.Remove(UrlInfo.MainURL);
                throw new AbortException("Failed to load URL info");
            }

            UrlInfo.ApplyFormatFilter();

            FileHostStream FHStream;

            if (UrlInfo.Urls.Any(x => x.SingleConnection))
            {
                FHStream = UrlInfo.Urls.First().Stream();
            }
            else
            {

                var ValidExt = UrlInfo.Urls.Select(x => x.Filename?.ToLowerInvariant()).Where(FName =>
                   string.IsNullOrWhiteSpace(FName)
                || FName.EndsWith(".rar")
                || FName.EndsWith(".7z") 
                || FName.EndsWith(".pkg")).Any();

                if (!ValidExt)
                {
                    if (UrlInfo.Urls.Length == 1)
                    {
                        await MessageBox.ShowAsync(Language.L("Msg.UnsupportedFormat"), "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return null;
                    }

                    var Rar = UrlInfo.Urls.SortRarFiles().ToArray();
                    var SevenZip = UrlInfo.Urls.Sort7zFiles().ToArray();

                    if (Rar.Length > 0)
                    {
                        SourcePackage = Rar.First().URL;
                        UrlInfo.Urls = Rar;
                    }

                    if (SevenZip.Length > 0)
                    {
                        SourcePackage = SevenZip.First().URL;
                        UrlInfo.Urls = SevenZip;
                    }

                    if (UrlInfo.Urls.Any(x => x.Filename.EndsWith(".pkg", StringComparison.InvariantCultureIgnoreCase)))
                    {
                        SourcePackage = UrlInfo.Links.First();
                    }

                    URLAnalyzer.URLInfos[SourcePackage] = UrlInfo;
                }
                else
                {
                    SourcePackage = UrlInfo.Links.First();
                }

                FHStream = new FileHostStream(SourcePackage);
            }
            
            return FHStream;
        }

        private async void ModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
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
                case "ShowTransferProgress":
                    App.Config.ShowTransferProgress = Model.ShowTransferProgress;
                    break;
                case "ShowError":
                    App.Config.ShowError = Model.ShowError;
                    PS4Server.EnableLog(Model.ShowError);

                    if (Model.ShowError)
                        await MessageBox.ShowAsync(Language.F("Msg.LogEnabled", Path.Combine(App.WorkingDirectory, "DPIServer.log")), "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    break;
                case "PS4IP":
                    App.Config.PSIP = Model.PS4IP;
                    break;
                case "PCIP":
                    App.Config.PCIP = Model.PCIP;
                    break;
                case "UseAllDebrid":
                    App.Config.UseAllDebrid = Model.UseAllDebrid;
                    break;
                case "UseRealDebrid":
                    App.Config.UseRealDebrid = Model.UseRealDebrid;
                    break;
                case "UseDebridLink":
                    App.Config.UseDebridLink = Model.UseDebridLink;
                    break;
                case "AllDebridApiKey":
                    App.Config.AllDebridApiKey = Model.AllDebridApiKey;
                    break;
                case "RealDebridApiKey":
                    App.Config.RealDebridApiKey = Model.RealDebridApiKey;
                    break;
                case "DebridLinkApiKey":
                    App.Config.DebridLinkApiKey = Model.DebridLinkApiKey;
                    break;
                case "DHCPService":
                    App.Config.EnableDHCP = Model.DHCPService;
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
                                await MessageBox.ShowAsync(Language.L("Msg.CNLServerStartFail"), "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            });
                        }
                    }
                    break;
                case "CurrentURL":
                    UrlChanged(Model.CurrentURL);
                    return;
            }

            if (App.IsAndroid)
                App.SaveSettings();
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

        private void BtnTransferProgressOnClick(object? sender, RoutedEventArgs e)
        {
            if (Model == null)
                return;

            Model.ShowTransferProgress = !Model.ShowTransferProgress;
        }

        private void BtnErrorLogOnClick(object? sender, RoutedEventArgs e)
        {
            if (Model == null)
                return;

            Model.ShowError = !Model.ShowError;
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
            
            Model.UseAllDebrid = !Model.UseAllDebrid;
        }
        
        private void BtnRealDebirdEnabledOnClick(object? sender, RoutedEventArgs e)
        {
            if (Model == null)
                return;
            
            Model.UseRealDebrid = !Model.UseRealDebrid;
        }

        private void BtnDebirdLinkEnabledOnClick(object? sender, RoutedEventArgs e)
        {
            if (Model == null)
                return;

            Model.UseDebridLink = !Model.UseDebridLink;
        }

        private async Task<bool> Install(string URL, bool Silent)
        {
            if (string.IsNullOrWhiteSpace(App.Config.PSIP) || string.IsNullOrWhiteSpace(App.Config.PCIP))
            {
                await MessageBox.ShowAsync(Parent, Language.L("Msg.FailedDetectPSIP"), "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            var OriStatus = Status.Text;
            SetLoadButtonState("Pushing");
            PackagesMenu.IsEnabled = false;
            btnLoad.IsEnabled = false;
            tbURL.IsEnabled = false;
            try
            {
                if (!await IPHelper.IsRPIOnline(App.Config.PSIP)) {
                    if (!await IPHelper.IsGoldHENOnline(App.Config.PSIP) && !Installer.Payload.ClientRunning)
                    {
                        if (!await IPHelper.IsEtaHenOnline(App.Config.PSIP))
                        {
                            await MessageBox.ShowAsync(Language.F("Msg.NoPayloadFound", App.Config.PSIP), "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return false;
                        }
                    }
                }

                return await Installer.PushPackage(App.Config, InputType, PKGStream!, URL, CurrentDecompressor, CurrentDecompressorVolumes, SetStatus, () => Status.Text, Silent);
            }
            catch
            {
                return false;
            }
            finally
            {
                await SetStatus(OriStatus);
                PackagesMenu.IsEnabled = true;
                btnLoad.IsEnabled = true;
                tbURL.IsEnabled = true;
                SetLoadButtonState("Install");
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

            bool ErrorIgnored = false;

            var Files = Installer.CurrentFileList ?? BatchList.Select(x => x.Source);

            foreach (var File in Files.ToArray())
            {
                var ContentSource = tbURL.Text;
                
                if (string.IsNullOrWhiteSpace(ContentSource))
                    ContentSource = File;
                else
                    Installer.EntryFileName = File;

                if (InputType.HasFlag(Source.SevenZip) || InputType.HasFlag(Source.RAR))
                {
                    Stream? Stream = null;
                    
                    try
                    {
                        var Entry = CurrentDecompressor!.Entries.Single(x => x.Key.EndsWith(File, StringComparison.InvariantCultureIgnoreCase));
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
                        using FileStream Stream = new FileStream(File, FileMode.Open);
                        Installer.CurrentPKG = Stream.GetPKGInfo() ?? throw new Exception();
                    } 
                    catch 
                    { 
                        continue; 
                    }
                }

                if (!await Install(ContentSource, true) && !ErrorIgnored)
                {
                    var Reply = await MessageBox.ShowAsync(Language.L("Msg.ContinueInstall"),"DirectPackageInstaller", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (Reply != DialogResult.Yes){
                        break;
                    }

                    ErrorIgnored = true;
                }

                if (Installer.Server != null)
                {
                    bool Decompressing() => Installer.Server?.Decompress.Tasks.Any(x => x.Value.Running) ?? false;
                    
                    while (Decompressing() || Installer.Server.Connections > 0 || PS4IdleSeconds() > 15)
                        await Task.Delay(5000);
                }
            }

            await MessageBox.ShowAsync(Language.L("Msg.PackagesSent"), "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static int PS4IdleSeconds()
        {
            if (Installer.Server?.LastRequest == null)
                return int.MaxValue;
            return (int)(DateTime.Now - (Installer.Server!.LastRequest!.Value)).TotalSeconds;
        }

        private void ListEntries(string[]? PKGList)
        {
            if (PKGList == null || PackagesMenu.Items == null)
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
                        Header = Path.GetFileName(Entry),
                        DataContext = Entry
                    };
                    
                    Item.Click += onInstallListItemClicked;

                    Items.Insert(0, Item);
                }
                PackagesMenu.Items.Clear();
                PackagesMenu.ItemsSource = Items;
            }
        }
        
        private void onInstallListItemClicked(object? sender, RoutedEventArgs e)
        {
            if (sender == null)
                return;

            var Item = (MenuItem)sender;
            var Entry = (string?)Item.DataContext;

            var OriSource = InputType;
            InputType = Source.NONE;

            if (OriSource.HasFlag(Source.RAR) || OriSource.HasFlag(Source.SevenZip))
            {
                var FileEntry = CurrentDecompressor!.Entries.First(x => x.Key.EndsWith(Entry, StringComparison.InvariantCultureIgnoreCase));
                Installer.EntryFileName = Path.GetFileName(FileEntry.Key);
                var Stream = new ReadSeekableStream(FileEntry.OpenEntryStream());

                App.Callback(() =>
                {
                    BtnLoadOnClick(Stream, null);

                    //Restore variables that is modified by the BtnLoadOnClick event
                    InputType = OriSource;
                    Installer.EntryFileName = Path.GetFileName(FileEntry.Key);
                });
                return;
            }

            tbURL.Text = Entry;
            App.Callback(() => BtnLoadOnClick(Entry, null));
        }

        private void OnLinksReceived((string[] Links, string Password) Info)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                App.Callback(() => OnLinksReceived(Info));
                return;
            }
            
            _ = URLAnalyzer.Analyze(Info.Links, false);

            tbURL.Text = Info.Links.First();
            App.Callback(() => BtnLoadOnClick(null, new RoutedEventArgs()));
        }

        private void DropEventEvent(object? sender, DragEventArgs e)
        {
            if (e.Data.Contains(DataFormats.Text))
            {
                var Links = e.Data.GetText()!.Split('\n', '\r')
                    .Where(x => !string.IsNullOrEmpty(x.Trim()) && x.Trim().StartsWith("http"))
                    .Select(x => x.Trim()).ToArray();

                _ = URLAnalyzer.Analyze(Links, false);

                App.Callback(() => {
                    Model.CurrentURL = Links.First();
                    App.Callback(() => BtnLoadOnClick(null, new RoutedEventArgs()));
                });
                return;
            }

            if (e.Data.Contains(DataFormats.FileNames))
            {
                var Files = e.Data.GetFileNames()!.ToArray();

                BatchList.Clear();
                BatchList.AddRange(Files.Select(x => (x, Path.GetFileName(x))));

                ListEntries(Files);

                App.Callback(() => {
                    Model.CurrentURL = Files.First();
                    App.Callback(() => BtnLoadOnClick(null, new RoutedEventArgs()));
                });
            }
        }

        private void DragOver(object? sender, DragEventArgs e)
        {
            e.DragEffects = e.DragEffects & (DragDropEffects.Copy | DragDropEffects.Link);

            if (!e.Data.Contains(DataFormats.Text) && !e.Data.Contains(DataFormats.FileNames))
                e.DragEffects = DragDropEffects.None;
        }

        private void UrlChanged(string Url)
        {
            InputType = Source.NONE;

            SetLoadButtonState((string.IsNullOrWhiteSpace(Url) && !File.Exists(Url)) ? "Open" : "Load");
            
            Installer.CurrentFileList = null;
            LastForcedSource = null;

            PackagesMenu.IsVisible = BatchList.Any();

            if (Url.Contains("\n"))
            {
                var Links = Url.Replace("\r\n", "\n").Split('\n')
                    .Select(x => x.Trim())
                    .Where(x => x.StartsWith("http", StringComparison.InvariantCultureIgnoreCase))
                    .ToArray();
                
                if (!Links.Any())
                {
                    App.Callback(() => Model!.CurrentURL = "");
                    return;
                }

                App.Callback(() =>
                {
                    Model!.CurrentURL = Links.First();
                    _ = App.RunInNewThread(() => _ = URLAnalyzer.Analyze(Links, false));
                });
                return;
            }

            if (Url.StartsWith("http", StringComparison.InvariantCultureIgnoreCase) && !Url.IsValidURL()) {
                int PathOrQueryPos = Url.IndexOfAny(new char[] { '/', '?' }, Url.IndexOf("://", StringComparison.Ordinal) + 3);
                
                if (PathOrQueryPos < 0)
                    return;
                
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
                if (NewUrl.IsValidURL())
                    Model!.CurrentURL = NewUrl;
            }

            PKGStream?.Close();
            PKGStream?.Dispose();
        }


        public async Task SetStatus(string Status) {
            
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

        private void ServerOnTransferProgressChanged(TransferProgressInfo Info)
        {
            if (!App.Config.ShowTransferProgress)
                return;

            var Now = DateTime.Now;
            if (!Info.Completed && (Now - LastTransferProgressUpdate).TotalMilliseconds < 500)
                return;

            LastTransferProgressUpdate = Now;
            var ProgressStatus = FormatTransferProgress(Info);

            App.Callback(() =>
            {
                if (App.Config.ShowTransferProgress)
                    Status.Text = ProgressStatus;
            });
        }

        private static string FormatTransferProgress(TransferProgressInfo Info)
        {
            var Sent = TransferProgressInfo.FormatBytes(Info.BytesSent);
            var Total = TransferProgressInfo.FormatBytes(Info.TotalBytes);

            if (Info.Completed)
                return Language.F("Status.TransferSent", Sent, Total);

            var Speed = TransferProgressInfo.FormatBytes(Info.BytesPerSecond);
            return Language.F("Status.TransferSending", Sent, Total, Info.Percent.ToString("P1"), Speed);
        }
        
        private async void RestartServer_OnClick(object? sender, RoutedEventArgs? e)
        {
            if (Model == null)
                return;
            
            try
            {
                Installer.Server?.Stop();

                PS4Server pS4Server = new PS4Server(Model.PCIP);
                Installer.Server = pS4Server;
                Installer.Server.Start();
            }
            catch (Exception ex){
                await MessageBox.ShowAsync(Language.F("Msg.ServerRestartFail", ex), Language.L("Title.ServerError"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnExitOnClick(object? sender, RoutedEventArgs? e)
        {
            App.SaveSettings();
            TempHelper.Clear();
            Environment.Exit(0);
        }
        
        private void tbURLOnGotFocus(object? sender, GotFocusEventArgs e)
        {
            if (!App.IsAndroid)
                return;
            
            App.Callback(() =>
            {
                tbURL.Text = App.GetClipboardText();
            });
        }

        private void PreviewGridOnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name != "Bounds")
                return;
            
            InLandscape = null;
            InvalidateMeasure();
        }

        bool? InLandscape = null;
        protected override Size MeasureCore(Size availableSize)
        {
            bool ChangeToLandscape = availableSize.Width > availableSize.Height; 
            if (ChangeToLandscape != InLandscape)
            {
                if (ChangeToLandscape)
                {
                    InLandscape = true;
                    PreviewGrid.RowDefinitions = new RowDefinitions()
                    {
                        new RowDefinition()
                    };
                    
                    var IconSize = PreviewGrid.Bounds.Height;
                    if (IconSize == 0)
                        IconSize = Double.PositiveInfinity;

                    var MaxWidth = availableSize.Width / 2;
                    
                    PreviewGrid.ColumnDefinitions = new ColumnDefinitions()
                    {
                        MaxWidth > IconSize ? new ColumnDefinition(IconSize, GridUnitType.Pixel) : new ColumnDefinition(),
                        new ColumnDefinition(App.IsAndroid ? 10f : 5f, GridUnitType.Pixel),
                        new ColumnDefinition()
                    };

                    IconBox[Grid.RowProperty] = 0;
                    IconBox[Grid.ColumnProperty] = 0;
                    
                    MainGridSplitter[Grid.RowProperty] = 0;
                    MainGridSplitter[Grid.ColumnProperty] = 1;
                    
                    PkgInfoGrid[Grid.ColumnProperty] = 2;
                    PkgInfoGrid[Grid.RowProperty] = 0;
                }
                else
                {
                    InLandscape = false;

                    var IconSize = PreviewGrid.Bounds.Width;
                    if (IconSize == 0)
                        IconSize = Double.PositiveInfinity;

                    var MaxHeight = availableSize.Height / 2;
                    
                    PreviewGrid.RowDefinitions = new RowDefinitions()
                    {
                        MaxHeight > IconSize ? new RowDefinition(IconSize, GridUnitType.Pixel) : new RowDefinition(),
                        new RowDefinition(App.IsAndroid ? 10f : 5f, GridUnitType.Pixel),
                        new RowDefinition()
                    };
                    PreviewGrid.ColumnDefinitions = new ColumnDefinitions()
                    {
                        new ColumnDefinition()
                    };

                    IconBox[Grid.RowProperty] = 0;
                    IconBox[Grid.ColumnProperty] = 0;
                    
                    MainGridSplitter[Grid.RowProperty] = 1;
                    MainGridSplitter[Grid.ColumnProperty] = 0;
                    
                    PkgInfoGrid[Grid.ColumnProperty] = 0;
                    PkgInfoGrid[Grid.RowProperty] = 2;
                }
            }
            else if (InLandscape != null)
            {
                var CurrentSize = InLandscape == true ? PreviewGrid.Bounds.Height : PreviewGrid.Bounds.Width;
                var MaxSize = InLandscape == true ? availableSize.Width / 2 : availableSize.Height / 2;
                if (CurrentSize > MaxSize)
                {
                    InLandscape = null;
                    return MeasureCore(availableSize);
                }
            }

            return base.MeasureCore(availableSize);
        }
    }
}
