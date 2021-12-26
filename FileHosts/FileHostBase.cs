using DirectPackageInstaller.IO;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web;

namespace DirectPackageInstaller.FileHosts
{
    public abstract class FileHostBase
    {
        string HostName { get; }

        public abstract bool IsValidUrl(string URL);
        public abstract DownloadInfo GetDownloadInfo(string URL);

        static WebClient HttpClient = new WebClient();

        protected const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/96.0.4664.93 Safari/537.36 Edg/96.0.1054.53";

        protected string DownloadString(string URL)
        {
            lock (HttpClient)
            {
                HttpClient.Headers["user-agent"] = UserAgent;
                HttpClient.Headers["referer"] = HttpUtility.UrlEncode(URL);

                return HttpClient.DownloadString(URL);
            }
        }

        protected (byte[] Data, WebHeaderCollection Headers) DownloadRequest(string URL)
        {
            lock (HttpClient)
            {
                HttpClient.Headers["user-agent"] = UserAgent;
                HttpClient.Headers["referer"] = HttpUtility.UrlEncode(URL);

                return (HttpClient.DownloadData(URL), HttpClient.ResponseHeaders);
            }
        }

        public static FileHostBase[] Hosts => new FileHostBase[] {
                new ZippyShare(), new Mediafire(), new GoogleDrive(), new PixelDrain()
        };
    }

    public struct DownloadInfo {
        public string Url;
        public List<(string Key, string Value)> Headers;
        public Cookie[] Cookies;
    }
}
