using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
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

        public static void DoEvents()
        {
            var Delay = new CancellationTokenSource();
            Delay.CancelAfter(100);
            
            Dispatcher.UIThread.MainLoop(Delay.Token);
        }


        public static void Callback(Action Callback)
        {
            Action CBCopy = Callback;
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await Task.Delay(50);
                CBCopy.Invoke();
            });
        }
        
        internal static Settings Config;

        internal static WebClientWithCookies HttpClient = new WebClientWithCookies();
        internal static bool IsRunningOnMono => Type.GetType("Mono.Runtime") != null;
        internal static bool IsUnix => (int)Environment.OSVersion.Platform == 4 || (int)Environment.OSVersion.Platform == 6 || (int)Environment.OSVersion.Platform == 128;
        internal static string WorkingDirectory => Environment.GetEnvironmentVariable("CD") ?? Directory.GetCurrentDirectory();
        internal static string SettingsPath => System.IO.Path.Combine(App.WorkingDirectory, "Settings.ini");

        internal static GitHub Updater = new GitHub("marcussacana", "DirectPackageInstaller", IsUnix ? "DirectPackageInstallerLinux" : "DirectPackageInstaller");
    }
}