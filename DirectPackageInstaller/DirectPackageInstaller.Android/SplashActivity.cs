using System.Linq;
using Android.App;
using Android.Content;
using Application = Android.App.Application;

namespace DirectPackageInstaller.Android
{
    [Activity(Theme = "@style/MyTheme.Splash", MainLauncher = true, NoHistory = true)]
    public class SplashActivity : Activity
    {
        ClipboardManager? ClipboardManager;

        protected override void OnStart()
        {
            RequestPermissions(new[] {
                "READ_EXTERNAL_STORAGE",
                "WRITE_EXTERNAL_STORAGE"
            }, 0);

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
            //App.GetFreeStorageSpace = () => BaseDir.UsableSpace;
            
            TempHelper.Clear();

            base.OnStart();
        }

        protected override void OnResume()
        {
            base.OnResume();

            StartActivity(new Intent(Application.Context, typeof(MainActivity)));
        }
    }
}