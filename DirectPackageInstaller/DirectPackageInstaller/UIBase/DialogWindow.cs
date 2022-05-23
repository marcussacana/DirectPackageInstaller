using System.Threading.Tasks;
using Avalonia.Controls;
using DirectPackageInstaller.ViewModels;
using DirectPackageInstaller.Views;

namespace DirectPackageInstaller.UIBase;

public abstract class DialogWindow : Window
{
    public async Task<DialogResult> ShowDialogAsync(Window? Parent = null)
    {
        await ShowDialog(Parent ?? MainWindow.Instance);
        
        return ((DialogModel)DataContext).Result;
    }
}