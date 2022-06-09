using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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

        if (Model == null)
            DataContext = new CookieManagerViewModel();
        
        Model.Result = DialogResult.Cancel;

        tbCookie = this.Find<TextBox>("tbCookie");
        tbCookie.GotFocus += tbCookieOnGotFocus;
        
        btnSave = this.Find<Button>("btnSave");
        btnSave.Click += Button_OnClick;
    }

    private void tbCookieOnGotFocus(object? sender, GotFocusEventArgs e)
    {
        if (!App.IsAndroid)
            return;
            
        App.Callback(() =>
        {
            var PrimaryClip = App.ClipboardManager?.PrimaryClip;
            if (PrimaryClip.ItemCount != 1)
                return;

            var ClipItem = PrimaryClip.GetItemAt(0);
            var Text =  ClipItem.CoerceToText(null);
                
            tbCookie.Text = Text;
        });
    }

    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        File.WriteAllText(CookiesPath, tbCookie.Text);

        if (Model != null)
            Model.Result = DialogResult.OK;

        if (Window == null)
            SingleView.ReturnView(this);
        else
            Window.Close();
    }
    
    static string CookiesPath => Path.Combine(Environment.GetEnvironmentVariable("CD") ?? AppDomain.CurrentDomain.BaseDirectory, "cookies.txt");
    public static List<Cookie> GetUserCookies(string Domain)
    {
        List<Cookie> Container = new List<Cookie>();

        if (!File.Exists(CookiesPath))
            return Container;

        var Lines = File.ReadAllLines(CookiesPath);

        foreach (var Line in Lines)
        {
            if (Line.StartsWith("#") || string.IsNullOrWhiteSpace(Line))
                continue;

            var Cookie = Line.Split('\t');

            if (Domain != null && !Cookie.First().ToLower().Contains(Domain.ToLower()))
                continue;

            //domain.com	FALSE	/path/	TRUE	0	Name	Value
            Container.Add(new Cookie(Cookie[5], Cookie[6], Cookie[2], Cookie[0]));
        }

        return Container;
    }
}