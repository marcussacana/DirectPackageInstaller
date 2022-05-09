using System;
using Avalonia.Controls;
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
        }

        private async void MainWindowOpened(object? sender, EventArgs e)
        {
            await View.OnShown(this);
        }
    }
}