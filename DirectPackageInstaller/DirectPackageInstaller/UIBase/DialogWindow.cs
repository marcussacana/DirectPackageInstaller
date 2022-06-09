using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using DirectPackageInstaller.ViewModels;
using DirectPackageInstaller.Views;

namespace DirectPackageInstaller.UIBase;

public abstract class DialogWindow : Window
{
    public static T CreateInstance<T>() where T : Window, new()
    {
        if (!Dispatcher.UIThread.CheckAccess())
            return Dispatcher.UIThread.InvokeAsync(CreateInstance<T>).ConfigureAwait(false).GetAwaiter().GetResult();

        return new T();
    }

    public DialogResult ShowDialogSync(Window? Parent = null)
    {
        if (!Dispatcher.UIThread.CheckAccess())
            return Dispatcher.UIThread.InvokeAsync(() => ShowDialogSync(Parent)).ConfigureAwait(false).GetAwaiter().GetResult();

        Extensions.ShowDialogSync(this, Parent ?? MainWindow.Instance);
        
        return ((DialogModel)DataContext).Result;
    }
    
    public async Task<DialogResult> ShowDialogAsync(Window? Parent = null)
    {
        if (!Dispatcher.UIThread.CheckAccess())
            return await Dispatcher.UIThread.InvokeAsync(async () => await ShowDialogAsync(Parent));
        
        await ShowDialog(Parent ?? MainWindow.Instance);
        
        return ((DialogModel)DataContext).Result;
    }
}