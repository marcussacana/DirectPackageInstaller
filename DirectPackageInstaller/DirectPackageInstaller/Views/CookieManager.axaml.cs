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
            
            ((CookieManagerViewModel)View.DataContext).CookiesPath = CookiesPath;
        }

        static string CookiesPath => Path.Combine(Environment.GetEnvironmentVariable("CD") ?? AppDomain.CurrentDomain.BaseDirectory, "cookies.txt");
        public static List<Cookie> GetUserCookies(string Domain)
        {
            List<Cookie> Container = new List<Cookie>();

            if (!File.Exists(CookiesPath))
                return Container;

            var Lines = File.ReadAllLines(CookiesPath);

            foreach (var Line in Lines)
            {
                if (Line.StartsWith("#") || string.IsNullOrWhiteSpace(Line))
                    continue;

                var Cookie = Line.Split('\t');

                if (Domain != null && !Cookie.First().ToLower().Contains(Domain.ToLower()))
                    continue;

                //domain.com	FALSE	/path/	TRUE	0	Name	Value
                Container.Add(new Cookie(Cookie[5], Cookie[6], Cookie[2], Cookie[0]));
            }

            return Container;
        }
    }
}