using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using DirectPackageInstaller.UIBase;
using DirectPackageInstaller.ViewModels;
using DynamicData;

namespace DirectPackageInstaller.Views
{
    public partial class LinkList : DialogWindow
    {
        LinkListViewModel? Model => (LinkListViewModel?)View.DataContext;
        
        public string[]? Links => Model?.Links.Select(x => x.Content).ToArray();
        public string? Password => Model?.Password;
        
        public LinkList(bool Multipart, bool Encrypted, string FirstUrl) : this()
        {
            if (Model != null)
            {
                Model.MainUrl = FirstUrl;
                Model.HasPassword = Encrypted;
                Model.IsMultipart = Multipart;
            }

            View.Initialized(this);
        }

        public LinkList()
        {
            InitializeComponent();

            View = this.Find<LinkListView>("View");

            DataContext = new LinkListViewModel();
            if (Model == null)
                View.DataContext = (LinkListViewModel)DataContext;
        }
    }
}