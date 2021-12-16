using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace DirectPackageInstaller.FileHosts
{
    class GoogleDrive : FileHostBase
    {
        public override DownloadInfo GetDownloadInfo(string URL)
        {
            if (!IsValidUrl(URL))
                throw new Exception("Invalid Url");

            var Response = DownloadRequest($"https://drive.google.com/u/0/uc?id={GetFileID(URL)}&export=download");

            List<Cookie> Cookies = new List<Cookie>();

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
                throw new Exception();

            HTML = HTML.Substring("goog-inline-block jfk-button jfk-button-action");

            string DownURL = HttpUtility.HtmlDecode(HTML.Substring("href=\"", "\">"));
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
