using Android.App;
using Android.Content.PM;
using Avalonia.Android;
using Avalonia;

namespace DirectPackageInstaller.Android
{
    [Activity(Label = "DirectPackageInstaller.Android", Theme = "@style/MyTheme.NoActionBar", Icon = "@drawable/icon",
        LaunchMode = LaunchMode.SingleInstance,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
    public class MainActivity : AvaloniaActivity<App>
    {
        protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        {
            return base.CustomizeAppBuilder(builder);
        }

        protected override void OnDestroy()
        {
            TempHelper.Clear();
            base.OnDestroy();
        }
    }
}