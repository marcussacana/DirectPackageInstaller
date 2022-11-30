using System;
using System.Linq;
using Android.App;
using Android.Content;
using Android.OS;
using Avalonia;
using Avalonia.Android;
using Avalonia.ReactiveUI;
using Java.IO;
using Application = Android.App.Application;
using Uri = Android.Net.Uri;

namespace DirectPackageInstaller.Android
{
    [Activity(Theme = "@style/MyTheme.Splash", MainLauncher = true, NoHistory = true)]
    public class SplashActivity : AvaloniaSplashActivity<App>
    {
        ClipboardManager? ClipboardManager;
        protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        {
            return base.CustomizeAppBuilder(builder).UseReactiveUI();
        }

        protected override void OnCreate(Bundle? savedInstanceState)
        {

            base.OnCreate(savedInstanceState);
        }

        protected override void OnStart()
        {
            ClipboardManager = (ClipboardManager) GetSystemService(ClipboardService);

            App.GetClipboardText = () =>
            {
                var PrimaryClip = ClipboardManager.PrimaryClip;
                if (PrimaryClip.ItemCount != 1)
                    return null;

                var ClipItem = PrimaryClip.GetItemAt(0);
                return ClipItem.CoerceToText(null);
            };

            var CacheDirs = Application.GetExternalCacheDirs();
            
            var ExtCacheDir = CacheDirs?.First();
            var SDCardDir = CacheDirs?.Length > 1 ? CacheDirs.Skip(1).MaxBy(x => x.FreeSpace) : null;
            
            var BaseDir = Application.GetExternalFilesDir(null);

            App._IsAndroid = true;
            App._WorkingDir = BaseDir?.AbsolutePath;
            App.AndroidCacheDir = ExtCacheDir?.AbsolutePath;
            App.AndroidSDCacheDir = SDCardDir?.AbsolutePath;
            App.GetRootDirPermission = GetStorageAccess;
            //App.GetFreeStorageSpace = () => BaseDir.UsableSpace;

            if (string.IsNullOrWhiteSpace(App.AndroidCacheDir))
                throw new FileNotFoundException("Failed to find the cache directory");
            
            TempHelper.Clear();

            base.OnStart();
        }

        public void GetStorageAccess()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
            {
                if (!global::Android.OS.Environment.IsExternalStorageManager)
                {
                    try
                    {
               
                        Uri? uri = Uri.Parse("package:" + PackageName);
                        Intent intent = new Intent(global::Android.Provider.Settings.ActionManageAppAllFilesAccessPermission, uri);
                        StartActivity(intent);
                    }
                    catch (Exception ex)
                    {
                        Intent intent = new Intent();
                        intent.SetAction(global::Android.Provider.Settings.ActionManageAppAllFilesAccessPermission);
                        StartActivity(intent);
                    }
                }
            }
        }

        protected override void OnResume()
        {
            base.OnResume();

            StartActivity(new Intent(Application.Context, typeof(MainActivity)));
        }
    }
}