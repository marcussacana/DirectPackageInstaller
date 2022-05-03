using Avalonia.Controls;
using DirectPackageInstaller.ViewModels;
using DirectPackageInstaller.Views;

namespace DirectPackageInstaller.UIBase;

public abstract class DialogWindow : Window
{

    public DialogResult ShowDialog()
    {
        if ((DataContext as DialogModel) != null)
            ((DialogModel)DataContext).Window = this;
        
        ShowDialog(MainWindow.Instance);
        return ((DialogModel)DataContext).Result;
    }
}