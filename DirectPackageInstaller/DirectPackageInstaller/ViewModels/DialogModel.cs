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
        public DialogResult Result;
        public Window Window;
    }
}