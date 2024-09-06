//#define DEBUG_SINGLEVIEW

using System;
using System.Collections;
using System.ComponentModel;
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
using DirectPackageInstaller.IO;
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
            if (Updater.FinishUpdatePending())
            {
                Process.Start(Updater.FinishUpdate());
                Environment.Exit(0);
                return;
            }

            UnlockHeaders();

            if (!Directory.Exists(WorkingDirectory))
                Directory.CreateDirectory(WorkingDirectory);
            
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
#if DEBUG && DEBUG_SINGLEVIEW
                IsSingleView = true;
                desktop.MainWindow = new DebugSingleView
                {
                    DataContext = new MainViewModel()
                };
#else
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainViewModel()
                };
#endif
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
            {
                IsSingleView = true;
                singleViewPlatform.MainView = new SingleView();
            }

            base.OnFrameworkInitializationCompleted();
        }

        public static void SaveSettings()
        {
            try
            {
                var IniWriter = new Ini(App.SettingsPath, "Settings");

                IniWriter.SetValue("PS4IP", Config.PS4IP);
                IniWriter.SetValue("PCIP", Config.PCIP);
                IniWriter.SetValue("SearchPS4", Config.SearchPS4.ToString());
                IniWriter.SetValue("ProxyDownload", Config.ProxyDownload.ToString());
                IniWriter.SetValue("SegmentedDownload", Config.SegmentedDownload.ToString());
                IniWriter.SetValue("UseAllDebrid", Config.UseAllDebrid.ToString());
                IniWriter.SetValue("AllDebridApiKey", Config.AllDebridApiKey);
                IniWriter.SetValue("UseRealDebrid", Config.UseRealDebrid.ToString());
                IniWriter.SetValue("RealDebridApiKey", Config.RealDebridApiKey);
                IniWriter.SetValue("EnableCNL", Config.EnableCNL.ToString());
                IniWriter.SetValue("Concurrency", SegmentedStream.DefaultConcurrency.ToString());
                IniWriter.SetValue("ShowError", Config.ShowError.ToString());
                IniWriter.SetValue("SkipUpdateCheck", Config.SkipUpdateCheck.ToString());
                
                IniWriter.Save();
            } catch {}
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
        

        public static Func<Action, Task> StartService = async (Act) =>
        {
            TaskCompletionSource CompletionSource = new TaskCompletionSource();
            var BGWorker = new BackgroundWorker();
            BGWorker.DoWork += (sender, e) =>
            {
                try
                {
                    Act?.Invoke();
                }
                finally
                {
                    BGWorker.Dispose();
                    CompletionSource.SetResult();
                }
            };
            BGWorker.RunWorkerAsync();
            await CompletionSource.Task;
        };
        
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
        
        internal static Settings Config = new Settings();

        internal static WebClientWithCookies WebClient = new WebClientWithCookies();
        internal static bool IsRunningOnMono => Type.GetType("Mono.Runtime") != null;
        internal static bool IsUnix => (int)Environment.OSVersion.Platform == 4 || (int)Environment.OSVersion.Platform == 6 || (int)Environment.OSVersion.Platform == 128;

        public static bool? _IsAndroid;
        internal static bool IsAndroid => _IsAndroid ??= SelfUpdate.MainExecutable == null;
        internal static bool IsOSX => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        public static Func<string?> GetClipboardText = null;

        internal static bool IsSingleView { get; private set; }

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

        public static string? _WorkingDir;
        
        public static string? AndroidCacheDir;
        public static string? AndroidSDCacheDir;

        public static bool UseSDCard = false;
        public static string? CacheBaseDirectory => (UseSDCard ? AndroidSDCacheDir : AndroidCacheDir) ?? AndroidCacheDir;
        
        public static string WorkingDirectory
        {
            get
            {               
                var Result = _WorkingDir;

                if (_WorkingDir == null)
                {
                    if (IsOSX)
                    {
                        _WorkingDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                        _WorkingDir = Path.Combine(_WorkingDir, "DirectPackageInstaller");
                    }

                    Result = _WorkingDir ??= Environment.GetEnvironmentVariable("CD") ?? Directory.GetCurrentDirectory();
                }

                if (string.IsNullOrWhiteSpace(Result.Trim('/', '\\')) && CurrentPlatform != OS.Android)
                    throw new Exception("FAILED TO GET THE WORKING DIRECTORY PATH");
                
                return Result;
            }
        }

        public static Func<long> GetFreeStorageSpace = () =>
        {
            if (CurrentPlatform == OS.Windows)
                return new DriveInfo(WorkingDirectory.Substring(0, 1)).AvailableFreeSpace;
            
            return new DriveInfo(CacheBaseDirectory ?? WorkingDirectory).AvailableFreeSpace;
        };

        public static Func<Task>? GetRootDirPermission;
        public static string RootDir
        {
            get
            {
                if (WorkingDirectory.Length > 2 && WorkingDirectory[1] == ':')
                    return WorkingDirectory.Substring(0, 3);

                if (IsAndroid)
                    return (UseSDCard ? AndroidRootSDDir : AndroidRootInternalDir) ?? throw new Exception();

                return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }
        }
        
        public static string? AndroidRootSDDir
        {
            get
            {
                if (IsAndroid)
                {                    
                    var Parent = Path.GetDirectoryName(AndroidSDCacheDir);
                    Parent = Path.GetDirectoryName(Parent);
                    Parent = Path.GetDirectoryName(Parent);
                    Parent = Path.GetDirectoryName(Parent);
                    
                    if (Parent != null && !Parent.EndsWith("/") && !Parent.EndsWith("\\"))
                        Parent += Path.DirectorySeparatorChar;
                    
                    return Parent;
                }

                return null;
            }
        }
        public static string? AndroidRootInternalDir { get; set; }

        internal static string SettingsPath => Path.Combine(WorkingDirectory, "Settings.ini");

        public static Action<string>? InstallApk;
    }
}