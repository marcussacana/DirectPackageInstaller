using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO.Enumeration;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using DirectPackageInstaller.ViewModels;
using DynamicData;
using DynamicData.Binding;

namespace DirectPackageInstaller.Views;

public partial class LinkListView : UserControl
{
    private Window Window;
    public LinkListViewModel? Model => (LinkListViewModel?)DataContext;
    public string[]? Links
    {
        get
        {
            if (!CheckAccess())
                return Dispatcher.UIThread.InvokeAsync(() => Links).Result;
                
            return Model?.Links.Distinct()
                .Where(x => !string.IsNullOrWhiteSpace(x.Content))
                .Select(x => x.Content).ToArray();
        }
    }
    public LinkListView()
    {
        InitializeComponent();

        btnOK = this.Find<Button>("btnOK");
        btnOK.Click += Button_OnClick;
    }

    public void Initialized(Window Window)
    {
        if (Model?.Links == null)
            return;
        
        if (Model.Links.Count == 0)
            Model.Links.Add(new LinkListViewModel.LinkEntry(Model.MainUrl));

        this.Window = Window;
    }
    private void TextBoxChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property.Name != "Text" || sender == null)
            return;
        
        for (int i = 0; i < Model.Links.Count; i++)
        {
            var Link = Model.Links[i].Content;
            if (Link.Contains("\n") || Link.Contains("\r"))
            {
                Model.Links.RemoveAt(i);
                var Links = Link.Split('\r', '\n')
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => new LinkListViewModel.LinkEntry(x));
                
                Model.Links.AddOrInsertRange(Links, i);
            }
            
            if (string.IsNullOrWhiteSpace(Link) && i + 1 < Model.Links.Count)
                Model.Links.RemoveAt(i);
        }

        var DistinctLinks = Model.Links.DistinctBy(x => x.Content).ToArray();

        if (DistinctLinks.Length != Model.Links.Count)
        {
            Model.Links.Clear();
            Model.Links.AddRange(DistinctLinks);
        }

        int Count = Model.Links.Count;
        for (int i = 0; i < Count; i++)
        {
            if (Count < 10)
                Model.Links[i].Name = $"Part {i + 1}";
            else if (Count < 100)
                Model.Links[i].Name = $"Part {i + 1:D2}";
            else
                Model.Links[i].Name = $"Part {i + 1:D3}";
        }
            
        if (Model.Links.Count == 0 || Model.Links.Last().Content != "")
            Model.Links.Add(new LinkListViewModel.LinkEntry(string.Empty));
    }
    
    public void SetInitialInfo(string[]? Links, string? Password)
    {
        if (Links != null)
        {
            Model.Links.Clear();
            Model.Links.AddRange(Links.Select(x=>new LinkListViewModel.LinkEntry(x)));
        }

        if (Password != null)
        {
            Model.Password = Password;
        }
    }
    private async void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        if (Model.IsMultipart ?? false) {
                
            var Lines = new string[] { Model.MainUrl }.Concat(
                Model.Links.Where(x => !string.IsNullOrWhiteSpace(x.Content) 
                                         && !Model.MainUrl.Equals(x.Content, StringComparison.OrdinalIgnoreCase)
                ).Select(x => x.Content)
            ).Distinct().ToArray();

            foreach (var Link in Lines)
            {
                if (!Link.IsValidURL())
                {
                    await MessageBox.ShowAsync("Invalid URL:\n" + Link, "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            Model.Links.Clear();
            Model.Links.AddRange(Lines.Select(x => new LinkListViewModel.LinkEntry(x)));
        }

        if ((Model.HasPassword ?? false) && string.IsNullOrEmpty(Model.Password))
        {
            await MessageBox.ShowAsync("Invalid Password", "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        
        ((DialogModel)Window.DataContext).Result = DialogResult.OK;

        if (App.IsSingleView)
        {
            SingleView.ReturnView(this);
        }
        else
        {
            App.Callback(() =>
            {
                Window.Hide();
                App.Callback(Window.Close);
            });
        }
    }
}