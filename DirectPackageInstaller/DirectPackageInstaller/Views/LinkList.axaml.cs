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

        public string[]? Links
        {
            get
            {
                if (!CheckAccess())
                    return Dispatcher.UIThread.InvokeAsync(() => Links).ConfigureAwait(false).GetAwaiter().GetResult();
                
                return Model?.Links.Distinct()
                    .Where(x => !string.IsNullOrWhiteSpace(x.Content))
                    .Select(x => x.Content).ToArray();
            }
        }
        private bool ViewInitialized;

        public string? Password
        {
            get
            {
                if (!CheckAccess())
                    return Dispatcher.UIThread.InvokeAsync(() => Password).ConfigureAwait(false).GetAwaiter().GetResult();

                return Model?.Password;
            }
        }

        public bool HasPassword
        {
            get
            {
                if (!CheckAccess())
                    return Dispatcher.UIThread.InvokeAsync(() => HasPassword).ConfigureAwait(false).GetAwaiter().GetResult();

                return Model?.HasPassword ?? false;
            }
            set => App.Callback(() => Model!.HasPassword = value);
        }
        public bool IsMultipart
        {
            get
            {
                if (!CheckAccess())
                    return  Dispatcher.UIThread.InvokeAsync(() => IsMultipart).ConfigureAwait(false).GetAwaiter().GetResult();

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
    }
}