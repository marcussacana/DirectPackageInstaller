using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using DirectPackageInstaller.Tasks;
using DirectPackageInstaller.ViewModels;

namespace DirectPackageInstaller.Views
{
    public partial class MainWindow : Window
    {
        public static MainWindow Instance;
        public MainWindow()
        {
            Instance = this;
            
            InitializeComponent();

            View = this.Find<MainView>("View");
            View.DataContext = new MainViewModel();
            
            Opened += MainWindowOpened;
            Closing += MainWindowClosing;
        }


        private async void MainWindowOpened(object? sender, EventArgs e)
        {
#if DEBUG
            this.AttachDevTools();
#endif
            await View.OnShown(this);
        }
        
        private async void MainWindowClosing(object? sender, CancelEventArgs e)
        {
            if (Installer.Server.Connections > 0)
            {
                if (await MessageBox.ShowAsync("The PS4 is still downloading\nDo you really wanna exit?", "DirectPackageInstaller", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
            }
            try
            {
                var IniWriter = new Ini(App.SettingsPath, "Settings");

                IniWriter.SetValue("PS4IP", App.Config.PS4IP);
                IniWriter.SetValue("PCIP", App.Config.PCIP);
                IniWriter.SetValue("SearchPS4", App.Config.SearchPS4.ToString());
                IniWriter.SetValue("ProxyDownload", App.Config.ProxyDownload.ToString());
                IniWriter.SetValue("SegmentedDownload", App.Config.SegmentedDownload.ToString());
                IniWriter.SetValue("UseAllDebrid", App.Config.UseAllDebrid.ToString());
                IniWriter.SetValue("AllDebridApiKey", App.Config.AllDebridApiKey);
                IniWriter.Save();
            } catch {}
        }
    }
}