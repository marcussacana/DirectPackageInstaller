using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using DirectPackageInstaller.Host;
using DirectPackageInstaller.Tasks;
using DirectPackageInstaller.ViewModels;
using LibOrbisPkg.PKG;

namespace DirectPackageInstaller.Views
{
    public partial class MainView : UserControl
    {
        private string SettingsPath => System.IO.Path.Combine(App.WorkingDirectory, "Settings.ini");
        public MainViewModel? Model => (MainViewModel?)DataContext;
        public MainView()
        {
            InitializeComponent();

            PackagesMenu = this.Find<MenuItem>("PackagesMenu");

            btnProxyDownload = this.Find<MenuItem>("btnProxyDownload");
            btnRestartServer = this.Find<MenuItem>("btnRestartServer");
            btnAllDebirdEnabled = this.Find<MenuItem>("btnAllDebirdEnabled");
            btnSegmentedDownload = this.Find<MenuItem>("btnSegmentedDownload");
            btnLoad = this.Find<Button>("btnLoad");

            btnRestartServer.Click += RestartServer_OnClick;
            btnProxyDownload.Click += BtnProxyDownloadOnClick;
            btnAllDebirdEnabled.Click += BtnAllDebirdEnabledOnClick;
            btnSegmentedDownload.Click += BtnSegmentedDownloadOnClick;
        }
        public async Task OnShown(MainWindow Parent)
        {
            if (Model == null)
                return;
            
            if (App.Updater.HaveUpdate() && await MessageBox.ShowAsync(Parent, "Update found, Update now?", "DirectPackageInstaller", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                App.Updater.Update();
                return;
            }
            
             if (File.Exists(SettingsPath))
            {
                App.Config = new Settings();
                var IniReader = new Ini(SettingsPath, "Settings");

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
                             Installer.StartServer(Model.PS4IP, Model.PCIP);
                         }
                     });
                 };
             }

             if (string.IsNullOrEmpty(App.Config.PS4IP) || !Locator.IsValidPS4IP(Model.PS4IP))
                 new Thread(() => Locator.Locate(string.IsNullOrEmpty(App.Config.PS4IP))).Start();

             if (!string.IsNullOrEmpty(App.Config.PS4IP))
                 new Thread(() => Installer.StartServer(Model.PS4IP, Model.PCIP)).Start();
             
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

       

        Stream PKGStream;
        
        PkgReader PKGParser;
        Pkg PKG;

        bool Fake;

        Source InputType = Source.NONE;

        string LastForcedSource = null;
        
        void UrlChanged(string Url)
        {
            InputType = Source.NONE;
            
            btnLoad.Content = (string.IsNullOrWhiteSpace(Url) || File.Exists(Url)) ? "Open" : "Load";
            Installer.CurrentFileList = null;
            LastForcedSource = null;

            PackagesMenu.IsVisible = false;

            if (Url.StartsWith("http") && !Uri.IsWellFormedUriString(Url, UriKind.Absolute)) {
                int PathOrQueryPos = Url.IndexOfAny(new char[] { '/', '?' }, Url.IndexOf("://") + 3);
                var Host = Url.Substring(0, PathOrQueryPos);

                var PathAndQuery = Url.Substring(PathOrQueryPos);


                var PathOnly = Uri.UnescapeDataString(PathAndQuery.Split('?').First());
                var QueryOnly = PathAndQuery.Contains('?') ? PathAndQuery.Substring(PathAndQuery.IndexOf('?')) : "";

                PathOnly = Uri.EscapeDataString(PathOnly);

                Dictionary<string, string> PathReplaces = new Dictionary<string, string>()
                { { "%2f", "/" }, { "%2F", "/" }, { "[", "%5b" }, { "]", "%5d" }, { "%2b", "+" }, { "%2B", "+" },
                    { "%28", "(" }, { "%29", ")" } };

                Dictionary<string, string> QueryReplaces = new Dictionary<string, string>()
                    { { "%2f", "/" }, { "%2F", "/" }, { "[", "%5b" }, { "]", "%5d" }  };

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