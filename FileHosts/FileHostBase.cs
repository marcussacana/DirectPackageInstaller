using DirectPackageInstaller.IO;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web;

namespace DirectPackageInstaller.FileHosts
{
    abstract class FileHostBase
    {
        string HostName { get; }

        public abstract bool IsValidUrl(string URL);
        public abstract DownloadInfo GetDownloadInfo(string URL);

        static WebClient HttpClient = new WebClient();
        protected string DownloadString(string URL)
        {
            HttpClient.Headers["user-agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/96.0.4664.93 Safari/537.36 Edg/96.0.1054.53";
            HttpClient.Headers["referer"] = HttpUtility.UrlEncode(URL);

            return HttpClient.DownloadString(URL);
        }

        public static FileHostBase[] Hosts => new FileHostBase[] {
                new ZippyShare()
        };
    }

    struct DownloadInfo {
        public string Url;
        public List<(string Key, string Value)> Headers;
        public Cookie[] Cookies;
    }
}
