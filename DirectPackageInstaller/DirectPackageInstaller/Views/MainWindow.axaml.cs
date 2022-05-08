using System;
using Avalonia.Controls;

namespace DirectPackageInstaller.Views
{
    public partial class MainWindow : Window
    {
        public static MainWindow Instance;
        public MainWindow()
        {
            Instance = this;
            
            InitializeComponent();
            
            Opened += OnOpened;
        }

        private async void OnOpened(object? sender, EventArgs e)
        {
            await MessageBox.ShowAsync("Message", "Title", MessageBoxButtons.RetryIgnoreCancel, MessageBoxIcon.Question);
            await MessageBox.ShowAsync("This is a test\nMultiline example\nLine\nLine\nLine\nLine\nLine\nLine\nLine", "Title of Msg Box", MessageBoxButtons.OK, MessageBoxIcon.Information);
            MessageBox.ShowSync("Sync Msg", "lol", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}