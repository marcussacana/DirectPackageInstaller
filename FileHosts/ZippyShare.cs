using NCalc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DirectPackageInstaller.FileHosts
{
    class ZippyShare : FileHostBase
    {
        public string HostName => "Zippyshare";

        public override DownloadInfo GetDownloadInfo(string URL)
        {
            if (!IsValidUrl(URL))
                throw new Exception("Invalid Url");

            string Page = DownloadString(URL);

            Page = Page.Substring("document.getElementById('dlbutton')");
            Page = Page.Substring("= \"");

            string Exp = Page.Substring("(", ")");

            Page = Page.Substring("+ \"", "\";");

            var Result = (int)new Expression(Exp).Evaluate();

            string ResultUrl = URL.Replace("file.html", $"{Result}{Page}").Replace("/v/", "/d/");
            return new DownloadInfo()
            {
                Url = ResultUrl,
                Headers = new List<(string Key, string Value)>(),
                Cookies = new System.Net.Cookie[0]
            };
        }

        public override bool IsValidUrl(string URL)
        {
            //https://www16.zippyshare.com/v/OPfZOy2h/file.html
            return URL.Contains(".zippyshare.com") && URL.Contains("/v/") && URL.Contains("file.html");
        }
    }
}
