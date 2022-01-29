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
        public CookieManager()
        {
            InitializeComponent();
        }

        private void CookieManager_Shown(object sender, EventArgs e)
        {
            if (File.Exists("cookies.txt"))
                tbCookies.Lines = File.ReadAllLines("cookies.txt");
        }

        private void CookieManager_FormClosing(object sender, FormClosingEventArgs e)
        {
            File.WriteAllLines("cookies.txt", tbCookies.Lines);
        }

        public static List<Cookie> GetUserCookies(string Domain)
        {
            List<Cookie> Container = new List<Cookie>();

            if (!File.Exists("cookies.txt"))
                return Container;

            var Lines = File.ReadAllLines("cookies.txt");

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
