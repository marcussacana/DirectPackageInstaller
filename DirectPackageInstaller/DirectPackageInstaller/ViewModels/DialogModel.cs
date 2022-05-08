using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using Avalonia.Controls;
using ReactiveUI;

namespace DirectPackageInstaller.ViewModels
{
    public class DialogModel : ReactiveObject
    {
        public string Message { get; set; }
        public string Title { get; set; }
        
        public MessageBoxButtons Buttons;
        public MessageBoxIcon Icon;
        
        public DialogResult Result;
    }
}