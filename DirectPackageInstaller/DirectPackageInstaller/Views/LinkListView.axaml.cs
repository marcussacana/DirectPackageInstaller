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
using DirectPackageInstaller.ViewModels;
using DynamicData;
using DynamicData.Binding;

namespace DirectPackageInstaller.Views;

public partial class LinkListView : UserControl
{
    public LinkListViewModel? Model => (LinkListViewModel?)DataContext;
    public LinkListView()
    {
        InitializeComponent();

        btnOK = this.Find<Button>("btnOK");
        btnOK.Click += Button_OnClick;
    }

    public void Initialized()
    {
        if (Model?.Links == null)
            return;
        
        if (Model.Links.Count == 0)
            Model.Links.Add(new LinkListViewModel.LinkEntry(Model.MainUrl));
        
        
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
            
        if (Model.Links.Count == 0 || Model.Links.Last().Content != "")
            Model.Links.Add(new LinkListViewModel.LinkEntry(string.Empty));
    }

    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        if (Model.IsMultipart ?? false) {
                
            var Lines = new string[] { Model.MainUrl }.Concat(
                Model.Links.Where(x => !string.IsNullOrWhiteSpace(x.Content) 
                                         && !Model.MainUrl.Equals(x.Content, StringComparison.OrdinalIgnoreCase)
                ).Select(x => x.Content)
            ).Distinct().ToArray();

            foreach (var Link in Lines)
            {
                if (!Uri.IsWellFormedUriString(Link, UriKind.Absolute))
                {
                    MessageBox.Show("Invalid URL:\n" + Link, "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            Model.Links.Clear();
            Model.Links.AddRange(Lines.Select(x => new LinkListViewModel.LinkEntry(x)));
        }

        if ((Model.HasPassword ?? false) && string.IsNullOrEmpty(Model.Password))
        {
            MessageBox.Show("Invalid Password", "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        
        ((LinkListViewModel)Model.Window.DataContext).Result = DialogResult.OK;
        Model.Window.Close();
    }
}