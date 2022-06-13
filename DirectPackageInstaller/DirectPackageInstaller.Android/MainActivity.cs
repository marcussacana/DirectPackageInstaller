using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.Content;
using Avalonia.Android;
using Avalonia;
using Java.IO;
using Application = Android.App.Application;

namespace DirectPackageInstaller.Android
{
    [Activity(Label = "DirectPackageInstaller.Android", Theme = "@style/MyTheme.NoActionBar", Icon = "@drawable/icon",
        LaunchMode = LaunchMode.SingleInstance,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
    public class MainActivity : AvaloniaActivity<App>
    {
        public static int Instances = 0;
        
        protected override void OnCreate(Bundle savedInstanceState)
        {
            if (Instances == 0)
            {

                App.InstallApk = (Path) =>
                {
                    var Install = new Intent(Intent.ActionView);
                    var ApkFile = FileProvider.GetUriForFile(Application.Context, "com.marcussacana.DirectPackageInstaller.provider", new File(Path));
                    Install.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
                    Install = Install.SetDataAndType(ApkFile,"application/vnd.android.package-archive");
                    StartActivity(Install);
                };
            }
            Instances++;
            base.OnCreate(savedInstanceState);
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