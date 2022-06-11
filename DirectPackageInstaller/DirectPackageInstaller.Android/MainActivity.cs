using Android.App;
using Android.Content.PM;
using Android.OS;
using Avalonia.Android;
using Avalonia;

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