using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Net;
using Android.Net.Wifi;
using Android.OS;
using Android.Views;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Avalonia.Android;
using Avalonia;
using Java.Lang;
using Application = Android.App.Application;
using File = Java.IO.File;

namespace DirectPackageInstaller.Android
{
    [Activity(Label = "DirectPackageInstaller.Android", Theme = "@style/MyTheme.NoActionBar", Icon = "@drawable/icon",
        LaunchMode = LaunchMode.SingleInstance,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
    public class MainActivity : AvaloniaActivity<App>
    {
        public static int Instances = 0;
        
        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            
            if (Instances == 0)
            {
                ActivityCompat.RequestPermissions(this, new []{
                        Manifest.Permission.ReadExternalStorage,
                        Manifest.Permission.WriteExternalStorage,
                        Manifest.Permission.ManageExternalStorage
                    },
                    1
                );
                
                App.InstallApk = (Path) =>
                {
                    var Install = new Intent(Intent.ActionView);
                    var ApkFile = FileProvider.GetUriForFile(Application.Context, "com.marcussacana.DirectPackageInstaller.provider", new File(Path));
                    Install.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
                    Install = Install.SetDataAndType(ApkFile,"application/vnd.android.package-archive");
                    StartActivity(Install);
                };

                ForegroundService.StartService(this, null);

                await IgnoreBatteryOptimizations();
            }
            Instances++;
        }

        private Dictionary<int, TaskCompletionSource> Tasks = new();
        protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
        {
            if (Tasks.ContainsKey(requestCode))
                Tasks[requestCode].SetResult();
        }

        protected override async void OnPause()
        {
            base.OnPause();
            await IgnoreBatteryOptimizations();
        }

        public async Task StartActivityAndWait(Intent? Activity)
        {
            TaskCompletionSource Source = new TaskCompletionSource();
            int ID = Tasks.Count;
            Tasks[ID] = Source;
            
            StartActivityForResult(Activity, Tasks.Count - 1);

            await Source.Task;
        }
        
        public async Task IgnoreBatteryOptimizations()
        {
            PowerManager? PowerMan = (PowerManager?)GetSystemService(PowerService);
            if (!PowerMan?.IsIgnoringBatteryOptimizations(PackageName) ?? true)
            {
                Intent intent = new Intent();
                intent.SetAction(global::Android.Provider.Settings.ActionRequestIgnoreBatteryOptimizations);
                intent.SetData(Uri.Parse("package:" + PackageName));
                await StartActivityAndWait(intent);
                
                PowerMan = (PowerManager?)GetSystemService(PowerService);
            }
            
            var Wakelock = PowerMan?.NewWakeLock(WakeLockFlags.Partial, "PartialWakeLock");
            if (Wakelock != null)
            {
                Wakelock.SetReferenceCounted(false);
                Wakelock.Acquire();
            }

            var WifiMan = (WifiManager?) GetSystemService(WifiService);
            if (WifiMan != null)
            {
                var WifiLock = WifiMan.CreateWifiLock(WifiMode.FullHighPerf, "FullHighWifiLock");
                WifiLock?.SetReferenceCounted(false);
                WifiLock?.Acquire();

                var WifiBroadcastLock = WifiMan.CreateMulticastLock("MulticastWifiLock");
                WifiBroadcastLock?.SetReferenceCounted(false);
                WifiBroadcastLock?.Acquire();
            }


            //Window?.AddFlags(WindowManagerFlags.KeepScreenOn);
        }

        protected override void OnResume()
        {
            try
            {
                base.OnResume();
            }
            catch (Exception ex)
            {
                LogFatalError(ex);
            }
        }

        public static void LogFatalError(Exception ex)
        {
            try
            {
                System.IO.File.WriteAllText(Path.Combine(App.WorkingDirectory, "DPI-AndroidCrash.log"), ex.ToString());
            }
            catch
            {
            }
        }

        protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        {
            return base.CustomizeAppBuilder(builder);
        }

        protected override void OnDestroy()
        {
            Instances--;
            
            if (Instances <= 0)
            {
                App.SaveSettings();
                TempHelper.Clear();
            }

            base.OnDestroy();
        }
    }
}