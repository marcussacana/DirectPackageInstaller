//Yeah, 1Fichier is implemented, but I don't plan to support this host.
//because of this it is not explicitly mentioned in the readme.
//Don't fire a issue about problems about slow 1fichier downloads.
//It's slow and unstable

using DirectPackageInstaller.Proxy;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using DirectPackageInstaller.UIBase;
using DirectPackageInstaller.Views;

namespace DirectPackageInstaller.FileHosts
{
    class OneFichier : FileHostBase
    {
        public override string HostName => "1Fichier";

        public override DownloadInfo GetDownloadInfo(string URL)
        {
            URL = URL.Split('&').First();

            if (!IsValidUrl(URL))
                throw new Exception("Invalid Url");

            string FinalUrl;
            WebProxy Proxy = null;

            while (true)
            {
                try
                {
                    var HTML = DownloadString(URL);
                    if (HTML.Contains("Without subscription"))
                    {
                        App.HttpClient.Proxy = ProxyHelper.WebProxy;
                        continue;
                    }

                    bool HasPassword = HTML.Contains("Password");

                    string Pass = null;

                    if (URL.Contains("?"))
                    {
                        var Query = HttpUtility.ParseQueryString(URL.Substring(URL.IndexOf('?') + 1));
                        
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
                    
                    var PostData = $"adz={ADZ}&did=0&dl_no_ssl=on&dlinline=on";
                    
                    if (Pass != null)
                        PostData += $"&pass={Pass}";
                    
                    HTML = PostString(URL, "application/x-www-form-urlencoded", PostData);

                    Proxy = App.HttpClient.Proxy as WebProxy;
                    App.HttpClient.Proxy = null;

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
                    ("User-Agent", UserAgent),
                    ("Referer", HttpUtility.UrlEncode(URL))
                },
                Url = FinalUrl,
                Proxy = Proxy,
                SingleConnection = true
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
