using System;
using System.Collections.Generic;
using System.Text;
using ReactiveUI;

namespace DirectPackageInstaller.ViewModels
{
    public class CookieManagerViewModel : DialogModel
    {
        public string? CookieList { get; set; }
        public string? CookiesPath { get; set; }
    }
}