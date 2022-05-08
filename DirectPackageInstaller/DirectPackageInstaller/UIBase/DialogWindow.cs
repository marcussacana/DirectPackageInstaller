using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using DirectPackageInstaller.ViewModels;
using DirectPackageInstaller.Views;

namespace DirectPackageInstaller.UIBase;

public abstract class DialogWindow : Window
{

    public DialogResult ShowDialogSync(Window? Parent = null)
    {   
        if (!Dispatcher.UIThread.CheckAccess())
        {
            return Dispatcher.UIThread.InvokeAsync(() => ShowDialogSync(Parent)).GetAwaiter().GetResult();
        }
        
        using (var source = new CancellationTokenSource())
        {
            ShowDialog(Parent ?? MainWindow.Instance).ContinueWith(t => source.Cancel(), TaskScheduler.FromCurrentSynchronizationContext());
            Dispatcher.UIThread.MainLoop(source.Token);
        }
        
        return ((DialogModel)DataContext).Result;
    }
    public async Task<DialogResult> ShowDialogAsync(Window? Parent = null)
    {
        await ShowDialog(Parent ?? MainWindow.Instance);
        
        return ((DialogModel)DataContext).Result;
    }
}