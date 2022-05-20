using System;
using System.Collections.Generic;
using System.Text;
using ReactiveUI;

namespace DirectPackageInstaller.ViewModels
{
    public class SelectViewModel : DialogModel
    {
        private string[] _Options = null;
        public string[] Options
        {
            get => _Options; set => this.RaiseAndSetIfChanged(ref _Options, value);
        }
    }
}