using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using DirectPackageInstaller.ViewModels;

namespace DirectPackageInstaller
{
    public class ViewLocator : IDataTemplate
    {
        public bool Match(object? data)
        {
            return data is ViewModelBase;
        }

        Control? ITemplate<object?, Control?>.Build(object? param)
        {
            if (param is null)
                return null;

            var name = param.GetType().FullName!.Replace("ViewModel", "View");
            var type = Type.GetType(name);

            if (type != null)
            {
                return (Control)Activator.CreateInstance(type)!;
            }

            return new TextBlock { Text = name };
        }
    }
}