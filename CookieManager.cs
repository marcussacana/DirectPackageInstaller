using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows.Forms;

namespace DirectPackageInstaller
{
    public partial class CookieManager : Form
    {
        static string CookiesPath => Path.Combine(Environment.GetEnvironmentVariable("CD") ?? AppDomain.CurrentDomain.BaseDirectory, "cookies.txt");
        public CookieManager()
        {
            InitializeComponent();
        }

        private void CookieManager_Shown(object sender, EventArgs e)
        {
            if (File.Exists(CookiesPath))
                tbCookies.Lines = File.ReadAllLines(CookiesPath);
        }

        private void CookieManager_FormClosing(object sender, FormClosingEventArgs e)
        {
            File.WriteAllLines(CookiesPath, tbCookies.Lines);
        }

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
        
        private void tbCookies_KeyDown(object sender, KeyEventArgs e)
        {
            if (Program.IsUnix && e.KeyValue == 131089)
            {
                if (tbCookies.SelectionLength > 0)
                    tbCookies.SelectedText = Clipboard.GetText();
                else
                    tbCookies.Text = Clipboard.GetText();

                e.Handled = true;
            }
        }

        private void tbCookies_TextChanged(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(tbCookies.Text))
                return;


            var Replaced = tbCookies.Text.Replace("\r\n", "\n").Replace("\n", "\r\n");
            if (Replaced != tbCookies.Text)
                tbCookies.Text = Replaced;
        }
    }
}
