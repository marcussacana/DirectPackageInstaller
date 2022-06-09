using Android.App;
using Android.Content;
using Java.Security;
using Application = Android.App.Application;
using Permission = Android.Content.PM.Permission;

namespace DirectPackageInstaller.Android
{
    [Activity(Theme = "@style/MyTheme.Splash", MainLauncher = true, NoHistory = true)]
    public class SplashActivity : Activity
    {
        protected override void OnResume()
        {
            base.OnResume();
            
            RequestPermissions(new string[]
            {
                "READ_EXTERNAL_STORAGE",
                "WRITE_EXTERNAL_STORAGE",
                "INTERNET"
            }, 1);

            StartActivity(new Intent(Application.Context, typeof(MainActivity)));
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            EnforceCallingOrSelfPermission("READ_EXTERNAL_STORAGE", "Permission required to read your PKG files");
            EnforceCallingOrSelfPermission("WRITE_EXTERNAL_STORAGE", "Permission required to cache the PKG on your phone");
            EnforceCallingOrSelfPermission("INTERNET", "Permission required to contact your PS4 and open URLs");
            
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}