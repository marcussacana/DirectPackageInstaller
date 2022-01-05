//Yeah, 1Fichier is implemented, but I don't plan to support this host.
//because of this it is not explicitly mentioned in the readme.
//Don't fire a issue about problems about slow 1fichier downloads.
//It's slow and unstable

using DirectPackageInstaller.Proxy;
using System;
using System.Linq;
using System.Net;
using System.Web;
using static DirectPackageInstaller.Program;

namespace DirectPackageInstaller.FileHosts
{
    class OneFichier : FileHostBase
    {
        public override DownloadInfo GetDownloadInfo(string URL)
        {
            URL = URL.Split('&').First();

            if (!IsValidUrl(URL))
                throw new Exception("Invalid Url");

            string FinalUrl = null;
            WebProxy Proxy = null;

            while (true)
            {
                try
                {
                    HttpClient.Proxy = ProxyHelper.WebProxy;
                    var HTML = DownloadString(URL);
                    if (HTML.Contains("Without subscription"))
                        continue;

                    var ADZ = HTML.Substring("name=\"adz\"").Substring("value=\"", "\"");
                    HTML = PostString(URL, "application/x-www-form-urlencoded", $"adz={ADZ}&did=0&dl_no_ssl=on&dlinline=on");

                    Proxy = HttpClient.Proxy as WebProxy;
                    HttpClient.Proxy = null;

                    HTML = HTML.Substring("ct_warn");
                    HTML = HTML.Substring("<div").Substring("<a", "</a>");

                    FinalUrl = HTML.Substring("href=\"", "\"");
                    break;
                }
                catch { }
            }

            return new DownloadInfo()
            {
                Headers = new System.Collections.Generic.List<(string Key, string Value)>()
                {
                    ("user-agent", UserAgent),
                    ("referer", HttpUtility.UrlEncode(URL))
                },
                Url = FinalUrl,
                Proxy = Proxy
            };
        }

        public override bool IsValidUrl(string URL)
        {
            string[] Domains = new string[]
            {
                "1fichier.com/",
                "afterupload.com/",
                "cjoint.net/",
                "desfichiers.com/",
                "megadl.fr/",
                "mesfichiers.org/",
                "piecejointe.net/",
                "pjointe.com/",
                "tenvoi.com/",
                "dl4free.com/"
            };

            return Domains.Where(x => URL.ToLowerInvariant().Contains(x)).Any() && URL.Contains("/?");
        }
    }
}
