using System;
using System.Collections.Generic;
using System.Net;
using Avalonia.Controls;
using DirectPackageInstaller.ViewModels;

namespace DirectPackageInstaller.Views
{
    public partial class CookieManager : Window
    {
        public CookieManager()
        {
            InitializeComponent();
        }

        public static List<Cookie> GetUserCookies(string Domain)
        {
            throw new NotImplementedException();
        }

        public DialogResult ShowDialog()
        {
            ShowDialog();
            return ((CookieManagerViewModel) DataContext).Result;
        }
    }
}