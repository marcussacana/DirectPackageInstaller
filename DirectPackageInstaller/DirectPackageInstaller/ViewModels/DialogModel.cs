using System;
using System.Collections.Generic;
using System.Text;
using Avalonia.Controls;
using ReactiveUI;

namespace DirectPackageInstaller.ViewModels
{
    public class DialogModel : ViewModelBase
    {
        public DialogResult Result;
        public Window Window;
    }
}