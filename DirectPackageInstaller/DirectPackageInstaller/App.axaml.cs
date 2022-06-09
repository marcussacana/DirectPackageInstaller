using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
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
            AppDomain.CurrentDomain.UnhandledException += async (sender, args) =>
            {
                if (args.IsTerminating)
                {
                    var ErrorInfo = args.ExceptionObject.ToString();
                    File.WriteAllText("DPI-CARSH.log", ErrorInfo);
                }
            };
            
            ServicePointManager.UseNagleAlgorithm = true;
            ServicePointManager.MaxServicePointIdleTime = 100000;
            ServicePointManager.DefaultConnectionLimit = 100;
            
            AvaloniaXamlLoader.Load(this);
        }

        public static readonly SelfUpdate Updater = new SelfUpdate();
        public override void OnFrameworkInitializationCompleted()
        {
            if (!IsAndroid)
            {
                if (Updater.FinishUpdatePending())
                {
                    Process.Start(Updater.FinishUpdate());
                    Environment.Exit(0);
                    return;
                }
            }

            UnlockHeaders();

            if (!Directory.Exists(WorkingDirectory))
                Directory.CreateDirectory(WorkingDirectory);
            
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainViewModel()
                };
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
            {
                singleViewPlatform.MainView = new SingleView();
            }

            base.OnFrameworkInitializationCompleted();
        }
        
        /// <summary>
        /// We aren't kids microsoft, we shouldn't need this
        /// 
        /// This Hack allows set custom header names instead
        /// use the microsoft enforced API that make my job
        /// harden than the usual to implement all possibilities
        /// of the http headers where I need handle it.
        ///
        /// After .NET Core 3, we can't use reflection to modify
        /// the readonly field, and we deal with that by loading
        /// early an patched assembly that don't use the readonly
        /// attribute in the field that we want modify. :)
        ///
        /// Suck My Dick Microsoft.
        /// </summary>
        public static void UnlockHeaders()
        {
            try
            {
                //At the time writing this, the dnSpyEx don't support .NET 6, then
                //this custom assembly is manually patched to only remove the readonly
                //attribute from the HeaderInfoTable, in the future will be better
                //just load a pre-patched assembly with all headers already unlocked
                var Asm = Assembly.Load(DirectPackageInstaller.Resources.WebHeaderCollection);
                var tHashtable = Asm
                    .GetType("System.Net.HeaderInfoTable")
                    .GetFields(BindingFlags.NonPublic | BindingFlags.Static)
                    .Single(x => x.FieldType.Name == "Hashtable");

                var Table = (Hashtable)tHashtable.GetValue(null);
                foreach (var Key in Table.Keys.Cast<string>().ToArray())
                {
                    var HeaderInfo = Table[Key];
                    HeaderInfo.GetType().GetField("IsRequestRestricted", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(HeaderInfo, false);
                    HeaderInfo.GetType().GetField("IsResponseRestricted", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(HeaderInfo, false);
                    Table[Key] = HeaderInfo;
                }

                tHashtable.SetValue(null, Table);
            }
            catch { }
        }

        public static async Task DoEvents() => await Task.Delay(100);
        

        public static void Callback(Action Callback)
        {
            Action CBCopy = Callback;
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await Task.Delay(50);
                CBCopy.Invoke();
            });
        }
        
        
        /// <summary>
        /// If the action thorws an exception returns true, otherwise returns false
        /// </summary>
        public static async Task<bool> RunInNewThread(Action Function)
        {
            bool Failed = false;
            TaskCompletionSource CompletationSource = new TaskCompletionSource();
            Thread BackgroundThread = new Thread(() =>
            {
                try
                {
                    Function.Invoke();
                }
                catch
                {
                    Failed = true;
                }
                finally
                {
                    CompletationSource.SetResult();
                }
            });
            
            BackgroundThread.Start();
            
            await CompletationSource.Task;

            return Failed;
        }
        
        internal static Settings Config;

        internal static WebClientWithCookies HttpClient = new WebClientWithCookies();
        internal static bool IsRunningOnMono => Type.GetType("Mono.Runtime") != null;
        internal static bool IsUnix => (int)Environment.OSVersion.Platform == 4 || (int)Environment.OSVersion.Platform == 6 || (int)Environment.OSVersion.Platform == 128;

        private static bool? _IsAndroid;
        internal static bool IsAndroid => _IsAndroid ??= SelfUpdate.MainExecutable == null;
        internal static bool IsOSX => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        internal static OS CurrentPlatform 
        {
            get
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    return OS.OSX;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    return OS.Linux;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return OS.Windows;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
                    return OS.FreeBSD;
                if (IsAndroid)
                    return OS.Android;
                
                throw new PlatformNotSupportedException();
            }
        }
        
        public enum OS
        {
            OSX, Linux, Windows, FreeBSD, Android
        }

        private static string? _WorkingDir;
        internal static string WorkingDirectory => _WorkingDir ??= IsAndroid ? 
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "DirectPackageInstaller") :
            Environment.GetEnvironmentVariable("CD") ?? Directory.GetCurrentDirectory();
        internal static string SettingsPath => Path.Combine(WorkingDirectory, "Settings.ini");
    }
}