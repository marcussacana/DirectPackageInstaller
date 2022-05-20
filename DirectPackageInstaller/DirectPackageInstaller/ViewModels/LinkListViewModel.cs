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
        public bool? IsMultipart { get; set; }
        public bool? HasPassword { get; set; }

        public sealed class LinkEntry
        {
            public LinkEntry(string Content) => this.Content = Content;
            public string Content { get; set; }
        }
    }
}