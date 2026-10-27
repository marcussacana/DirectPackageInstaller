using System;
using System.ComponentModel;
using Acornima.Ast;
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
                        if (await MessageBox.ShowAsync(Language.L("Msg.PlaystationStillDownloading"), "DirectPackageInstaller", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                            return;

                        ForceClose = true;
                        Close();
                    });
                    e.Cancel = true;
                    return;
                }
            }
            catch { }

            View.Status.Text = Language.L("Status.ShuttingDown");

            App.SaveSettings();
            await Installer.Payload.StopServer();

            if (View.DHCP != null)
            {
                View.DHCP.Stop();
                await App.SetupDHCPNetwork();
            }
        }
    }
}