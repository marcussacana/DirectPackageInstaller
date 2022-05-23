using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using ReactiveUI;

namespace DirectPackageInstaller.ViewModels
{
    public sealed class LinkListViewModel : DialogModel
    {
        public ObservableCollection<LinkEntry> Links { get; set; } = new ObservableCollection<LinkEntry>();

        public string MainUrl { get; set; }
        
        public string Password { get; set; }

        private bool? _IsMultipart = null;
        public bool? IsMultipart
        {
            get => _IsMultipart;
            set => this.RaiseAndSetIfChanged(ref _IsMultipart, value);
        }

        public bool? HasPassword { get; set; }

        public sealed class LinkEntry : ReactiveObject
        {
            public LinkEntry(string Content) => this.Content = Content;
            public string Content { get; set; }

            private string _Name;
            public string Name
            {
                get => _Name;
                set => this.RaiseAndSetIfChanged(ref _Name, value);
            }
        }
    }
}