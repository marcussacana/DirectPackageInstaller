using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;

namespace DirectPackageInstaller.FileHosts
{
    class GoogleDrive : FileHostBase
    {
        public override string HostName => "GoogleDrive";

        bool CookieAsked = false;
        static List<Cookie> UserCookies = new List<Cookie>();

        public override DownloadInfo GetDownloadInfo(string URL)
        {
            if (!IsValidUrl(URL))
                throw new Exception("Invalid Url");

            List<Cookie> Cookies = new List<Cookie>();
            Cookies.AddRange(UserCookies);

            var Response = DownloadRequest($"https://drive.google.com/u/0/uc?id={GetFileID(URL)}&export=download", Cookies.ToArray());


            foreach (var Cookie in Response.Headers.GetValues("set-cookie"))
            {
                var Parts = Cookie.Split(';');
                if (!Parts.First().Contains("="))
                    continue;

                var Name = Parts.First().Split('=')[0];
                var Value = Parts.First().Substring("=");

                Cookies.Add(new Cookie(Name, Value, "/", ".google.com"));
            }

            var HTML = Encoding.UTF8.GetString(Response.Data);
            if (HTML.Contains("Quota exceeded"))
            {
                if (!CookieAsked)
                {
                    var OldCount = UserCookies.Count;
                    UserCookies = CookieManager.GetUserCookies("google.com");

                    if (OldCount == UserCookies.Count && (UserCookies.Count == 0 || !CookieAsked))
                    {
                        CookieAsked = true;
                        var CManager = new CookieManager();
                        CManager.ShowDialog();
                    }

                    return GetDownloadInfo(URL);
                }

                throw new Exception();
            }

            HTML = HTML.Substring("goog-inline-block jfk-button jfk-button-action");

            string DownURL = HttpUtility.HtmlDecode(HTML.Substring("href=\"", "\">")).Replace("/u/0", "");
            if (!DownURL.StartsWith("http"))
                DownURL = "https://drive.google.com/u/0" + DownURL;

            return new DownloadInfo()
            {
                Headers = new List<(string Key, string Value)>()
                {
                    ("referer", "https://drive.google.com/"),
                    ("user-agent", UserAgent)
                },
                Cookies = Cookies.ToArray(),
                Url = DownURL
            };
        }

        public override bool IsValidUrl(string URL)
        {
            return URL.Contains("drive.google.com") && GetFileID(URL) != null;
        }

        string GetFileID(string URL)
        {
            //https://drive.google.com/file/d/161nRA0mXrCfrSWW11rfg9g0nmCzaUR99/view
            //https://drive.google.com/u/0/uc?id=161nRA0mXrCfrSWW11rfg9g0nmCzaUR99&export=download
            //https://drive.google.com/open?id=161nRA0mXrCfrSWW11rfg9g0nmCzaUR99

            if (URL.Contains("id=") && URL.Contains("&export="))
                return URL.Substring("id=", "&export=");            

            if (URL.Contains("id="))
                return URL.Substring("id=");

            if (URL.Contains("/file/d/"))
                return URL.Substring("/file/d/", "/");

            return null;
        }
    }
}
