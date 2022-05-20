using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DirectPackageInstaller.ViewModels;

namespace DirectPackageInstaller.Views;

public partial class CookieManagerView : UserControl
{
    private Window? Window => (Window?)Parent;
    private CookieManagerViewModel? Model => (CookieManagerViewModel?)DataContext;
    public CookieManagerView()
    {
        InitializeComponent();

        if (Model != null)
            Model.Result = DialogResult.Cancel;
        
        btnSave = this.Find<Button>("btnSave");
        btnSave.Click += Button_OnClick;
    }
    
    protected void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        if (Model == null || Window == null)
            throw new NullReferenceException();
        
        File.WriteAllText(Model.CookiesPath, Model.CookieList); 
        Model.Result = DialogResult.OK;
        Window.Close();
    }
}