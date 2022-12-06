using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Web;

namespace DirectPackageInstaller.FileHosts
{
    public abstract class FileHostBase
    {
        public abstract string HostName { get; }
        public abstract bool Limited { get; }

        public abstract  bool IsValidUrl(string URL);
        public abstract DownloadInfo GetDownloadInfo(string URL);

        public const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/96.0.4664.93 Safari/537.36 Edg/96.0.1054.53";

        protected string DownloadString(string URL, Cookie[]? Cookies = null)
        {
            lock (App.HttpClient)
            {
                App.HttpClient.Headers.Clear();
                App.HttpClient.Headers["user-agent"] = UserAgent;
                App.HttpClient.Headers["referer"] = HttpUtility.UrlEncode(URL);

                App.HttpClient.Container = new CookieContainer();
                if (Cookies != null)
                {
                    foreach (var Cookie in Cookies)
                    {
                        try
                        {
                            App.HttpClient.Container.Add(Cookie);
                        }
                        catch { }
                    }
                }

                return App.HttpClient.DownloadString(URL);
            }
        }
        protected WebHeaderCollection? Head(string URL, Cookie[]? Cookies = null)
        {
            lock (App.HttpClient)
            {
                App.HttpClient.Headers.Clear();
                App.HttpClient.Headers["user-agent"] = UserAgent;
                App.HttpClient.Headers["referer"] = HttpUtility.UrlEncode(URL);

                App.HttpClient.Container = new CookieContainer();
                
                if (Cookies != null)
                {
                    foreach (var Cookie in Cookies)
                    {
                        try
                        {
                            App.HttpClient.Container.Add(Cookie);
                        }
                        catch { }
                    }
                }

                try
                {
                    var Response = App.HttpClient.OpenRead(URL);
                    Response.Close();

                    return App.HttpClient.ResponseHeaders;
                }
                catch
                {
                    return null;
                }
            }
        }
        protected string PostString(string URL, string ContentType, string Data, Cookie[]? Cookies = null)
        {
            lock (App.HttpClient)
            {
                App.HttpClient.Headers.Clear();
                App.HttpClient.Headers["content-type"] = ContentType;
                App.HttpClient.Headers["user-agent"] = UserAgent;
                App.HttpClient.Headers["referer"] = HttpUtility.UrlEncode(URL);

                App.HttpClient.Container = new CookieContainer();
                if (Cookies != null)
                {
                    foreach (var Cookie in Cookies)
                    {
                        try
                        {
                            App.HttpClient.Container.Add(Cookie);
                        }
                        catch { }
                    }
                }

                return App.HttpClient.UploadString(URL, Data);
            }
        }

        protected (byte[] Data, WebHeaderCollection? Headers) DownloadRequest(string URL, Cookie[]? Cookies = null)
        {
            lock (App.HttpClient)
            {
                App.HttpClient.Headers.Clear();
                App.HttpClient.Headers["user-agent"] = UserAgent;
                App.HttpClient.Headers["referer"] = HttpUtility.UrlEncode(URL);

                App.HttpClient.Container = new CookieContainer();
                if (Cookies != null)
                {
                    foreach (var Cookie in Cookies)
                    {
                        try
                        {
                            App.HttpClient.Container.Add(Cookie);
                        }
                        catch { }
                    }
                }

                return (App.HttpClient.DownloadData(URL), App.HttpClient.ResponseHeaders);
            }
        }

        public static FileHostBase[] Hosts => new FileHostBase[] {
                new ZippyShare(), new Mediafire(), new GoogleDrive(),
                new PixelDrain(), new AllDebrid(), new OneFichier()
        };
    }

    public struct DownloadInfo {
        public string Url;
        public List<(string Key, string Value)> Headers;
        public Cookie[] Cookies;
        public WebProxy Proxy;
    }
}
