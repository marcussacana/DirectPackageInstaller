using System;
using System.IO;
using System.Reflection;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DirectPackageInstaller.ViewModels;
using DirectPackageInstaller.Views;

namespace DirectPackageInstaller
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainViewModel()
                };
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
            {
                singleViewPlatform.MainView = new MainView
                {
                    DataContext = new MainViewModel()
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
        
        internal static Settings Config;

        internal static WebClientWithCookies HttpClient = new WebClientWithCookies();

        internal static bool IsUnix => (int)Environment.OSVersion.Platform == 4 || (int)Environment.OSVersion.Platform == 6 || (int)Environment.OSVersion.Platform == 128;
        internal static string WorkingDirectory => Environment.GetEnvironmentVariable("CD") ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        internal static GitHub Updater = new GitHub("marcussacana", "DirectPackageInstaller", IsUnix ? "DirectPackageInstallerLinux" : "DirectPackageInstaller");
    }
}