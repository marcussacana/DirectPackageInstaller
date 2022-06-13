using System;
using System.Linq;
using Android.App;
using Android.Content;
using Application = Android.App.Application;
using Uri = Android.Net.Uri;

namespace DirectPackageInstaller.Android
{
    [Activity(Theme = "@style/MyTheme.Splash", MainLauncher = true, NoHistory = true)]
    public class SplashActivity : Activity
    {
        ClipboardManager? ClipboardManager;

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
            
            App._WorkingDir = BaseDir?.AbsolutePath;
            App.AndroidCacheDir = ExtCacheDir?.AbsolutePath;
            App.AndroidSDCacheDir = SDCardDir?.AbsolutePath;
            App.GetRootDirPermission = GetStorageAccess;
            //App.GetFreeStorageSpace = () => BaseDir.UsableSpace;
            
            TempHelper.Clear();

            base.OnStart();
        }

        public void GetStorageAccess()
        {
            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.R)
            {
                if (!global::Android.OS.Environment.IsExternalStorageManager)
                {
                    try
                    {
               
                        Uri uri = Uri.Parse("package:" + PackageName);
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