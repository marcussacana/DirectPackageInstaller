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
            RequestPermissions(new string[]
            {
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

            var ExtCacheDir = Application.GetExternalCacheDirs().First();
            var BaseDir = Application.GetExternalFilesDir(null);
            
            App._WorkingDir = BaseDir.AbsolutePath;
            App.AndroidCacheDir = ExtCacheDir.AbsolutePath;
            App.GetFreeStorageSpace = () => BaseDir.UsableSpace;
            
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