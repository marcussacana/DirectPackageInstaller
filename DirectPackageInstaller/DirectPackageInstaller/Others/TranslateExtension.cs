using System;
using Avalonia.Markup.Xaml;

namespace DirectPackageInstaller
{
    /// <summary>
    /// XAML helper to plug a translated string straight into the interface:
    ///
    ///   xmlns:loc="clr-namespace:DirectPackageInstaller"
    ///   &lt;Label Content="{loc:Translate Key=Menu.Options}"/&gt;
    ///
    /// The value is resolved while the view is built, so it's only suitable for views
    /// that are created on demand (dialogs, window titles, ...). Long living views
    /// like the MainView re-apply their strings from code-behind on Language.Loaded.
    /// </summary>
    public class TranslateExtension : MarkupExtension
    {
        public TranslateExtension() { }

        public TranslateExtension(string Key) => this.Key = Key;

        public string? Key { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider) => Language.L(Key ?? string.Empty);
    }
}
