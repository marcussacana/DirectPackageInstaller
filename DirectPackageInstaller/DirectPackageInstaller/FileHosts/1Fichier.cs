using System;
using System.Linq;
using System.Net;
using System.Web;
using DirectPackageInstaller.UIBase;
using DirectPackageInstaller.Views;

namespace DirectPackageInstaller.FileHosts
{
    class OneFichier : FileHostBase
    {
        public override string HostName => "1Fichier";
        public override bool Limited => true;

        public override DownloadInfo GetDownloadInfo(string URL)
        {
            var FullUrl = URL;
            URL = URL.Split('&').First();

            if (!IsValidUrl(URL))
                throw new Exception("Invalid Url");

            string HTML = "";

            int Tries = 5;
            while (Tries-- > 0)
            {
                HTML = DownloadString(URL);
                if (HTML.Contains("Without subscription"))
                {
                    if (!ConnectionHelper.AllowReconnect)
                        throw new Exception();

                    ConnectionHelper.Reset().Wait();
                    continue;
                }
                break;
            }

            bool HasPassword = HTML.Contains("Password");

            string Pass = null;

            if (FullUrl.Contains("?"))
            {
                var Query = HttpUtility.ParseQueryString(FullUrl.Substring(FullUrl.IndexOf('?') + 1));

                if (Query.AllKeys.Contains("pass"))
                    Pass = Query["pass"];

                if (Query.AllKeys.Contains("password"))
                    Pass = Query["password"];
            }

            if (Pass == null && HasPassword)
            {
                var List = DialogWindow.CreateInstance<LinkList>();

                List.IsMultipart = false;
                List.HasPassword = true;
                List.Initialize();

                if (List.ShowDialogSync() != DialogResult.OK)
                    throw new Exception();

                Pass = List.Password;
            }


            var ADZ = HTML.Substring("name=\"adz\"").Substring("value=\"", "\"");

            var PostData = $"adz={ADZ}&did=0&dl_no_ssl=off&dlinline=on";

            if (Pass != null)
                PostData += $"&pass={Pass}";

            HTML = PostString(URL, "application/x-www-form-urlencoded", PostData);

            HTML = HTML.Substring("ct_warn");
            HTML = HTML.Substring("<div").Substring("<a", "</a>");

            var FinalUrl = HTML.Substring("href=\"", "\"");

            return new DownloadInfo()
            {
                Headers = new System.Collections.Generic.List<(string Key, string Value)>()
                {
                    ("User-Agent", UserAgent),
                    ("Referer", HttpUtility.UrlEncode(URL))
                },
                Url = FinalUrl
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
