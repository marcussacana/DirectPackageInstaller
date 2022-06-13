using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using DirectPackageInstaller.ViewModels;

namespace DirectPackageInstaller.Views
{
    public partial class DebugSingleView: Window
    {
        public static DebugSingleView Instance;
        public DebugSingleView()
        {
            Instance = this;
            
            InitializeComponent();

            View = this.Find<SingleView>("View");
            View.DataContext = new MainViewModel();
            
            Opened += MainWindowOpened;
            Closing += MainWindowClosing;
        }

        private async void MainWindowOpened(object? sender, EventArgs e)
        {
#if DEBUG
            this.AttachDevTools();
#endif
        }
        
        private void MainWindowClosing(object? sender, CancelEventArgs e)
        {
           App.SaveSettings();
        }
    }
}