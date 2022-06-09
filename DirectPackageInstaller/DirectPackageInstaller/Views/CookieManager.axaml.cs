using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Avalonia.Controls;
using DirectPackageInstaller.UIBase;
using DirectPackageInstaller.ViewModels;

namespace DirectPackageInstaller.Views
{
    public partial class CookieManager : DialogWindow
    {
        public CookieManager()
        {
            InitializeComponent();

            View = this.Find<CookieManagerView>("View");
            
            if (DataContext == null)
                DataContext = new CookieManagerViewModel();
            
            View.DataContext = DataContext;
        }

       
    }
}