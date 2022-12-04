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

        private bool ForceClose = false;
        
        private async void MainWindowClosing(object? sender, CancelEventArgs e)
        {
            try
            {
                bool PS4Connected = Installer.Server?.Connections > 0;
                PS4Connected |= (DateTime.Now - Installer.Server?.LastRequest)?.TotalSeconds < 5;

                if (PS4Connected && !ForceClose)
                {
                    App.Callback(async () =>
                    {
                        if (await MessageBox.ShowAsync("The PS4 is still downloading\nDo you really wanna exit?", "DirectPackageInstaller", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                            return;

                        ForceClose = true;
                        Close();
                    });
                    e.Cancel = true;
                    return;
                }
            } 
            catch {}

           App.SaveSettings();
           await Installer.Payload.StopServer();
        }
    }
}