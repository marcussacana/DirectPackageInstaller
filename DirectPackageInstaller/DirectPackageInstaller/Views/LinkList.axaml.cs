using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using DirectPackageInstaller.UIBase;
using DirectPackageInstaller.ViewModels;
using DynamicData;
using DynamicData.Binding;

namespace DirectPackageInstaller.Views
{
    public partial class LinkList : DialogWindow
    {
        LinkListViewModel? Model => (LinkListViewModel?)View.DataContext;

        private bool ViewInitialized;

        public string[]? Links => View.Links;
        public string? Password
        {
            get
            {
                if (!CheckAccess())
                    return Dispatcher.UIThread.InvokeAsync(() => Password).Result;

                return Model?.Password;
            }
        }

        public bool HasPassword
        {
            get
            {
                if (!CheckAccess())
                    return Dispatcher.UIThread.InvokeAsync(() => HasPassword).Result;

                return Model?.HasPassword ?? false;
            }
            set => App.Callback(() => Model!.HasPassword = value);
        }
        public bool IsMultipart
        {
            get
            {
                if (!CheckAccess())
                    return  Dispatcher.UIThread.InvokeAsync(() => IsMultipart).Result;

                return Model?.IsMultipart ?? false;
            }
            set => App.Callback(() => Model!.IsMultipart = value);
        }
        public LinkList(bool Multipart, bool? Encrypted, string FirstUrl) : this()
        {
            if (Model != null)
            {
                Model.MainUrl = FirstUrl;
                Model.HasPassword = Encrypted;
                Model.IsMultipart = Multipart;
            }

            ViewInitialized = true;
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

        
        public void Initialize()
        {
            if (ViewInitialized)
                return;
            
            ViewInitialized = true;
            App.Callback(() => View.Initialized(this));
        }

        public void SetInitialInfo(string[]? Links, string? Password) => View.SetInitialInfo(Links, Password);
    }
}