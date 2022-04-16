using Avalonia.Web.Blazor;

namespace DirectPackageInstaller.Web;

public partial class App
{
    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        WebAppBuilder.Configure<DirectPackageInstaller.App>()
            .SetupWithSingleViewLifetime();
    }
}