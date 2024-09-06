using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
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
            lock (App.WebClient)
            {
                App.WebClient.Headers.Clear();
                App.WebClient.Headers["user-agent"] = UserAgent;
                App.WebClient.Headers["referer"] = HttpUtility.UrlEncode(URL);

                App.WebClient.Container = new CookieContainer();
                if (Cookies != null)
                {
                    foreach (var Cookie in Cookies)
                    {
                        try
                        {
                            App.WebClient.Container.Add(Cookie);
                        }
                        catch { }
                    }
                }

                return App.WebClient.DownloadString(URL);
            }
        }
        protected WebHeaderCollection? HeadGet(string URL, Cookie[]? Cookies = null)
        {
            lock (App.WebClient)
            {
                App.WebClient.Headers.Clear();
                App.WebClient.Headers["user-agent"] = UserAgent;
                App.WebClient.Headers["referer"] = HttpUtility.UrlEncode(URL);

                App.WebClient.Container = new CookieContainer();
                
                if (Cookies != null)
                {
                    foreach (var Cookie in Cookies)
                    {
                        try
                        {
                            App.WebClient.Container.Add(Cookie);
                        }
                        catch { }
                    }
                }

                try
                {
                    var Response = App.WebClient.OpenRead(URL);
                    Response.Close();

                    return App.WebClient.ResponseHeaders;
                }
                catch
                {
                    return null;
                }
            }
        }
        protected HttpResponseHeaders? HeadPost(string URL, string ContentType, string Data, Cookie[]? Cookies = null)
        {
            using (var ReqHandler = new HttpClientHandler())
            {
                var container = new CookieContainer();
                ReqHandler.CookieContainer = container;
                ReqHandler.AllowAutoRedirect = false;
                using (HttpClient Client = new HttpClient(ReqHandler))
                {
                    try
                    {
                        using (var Message = new HttpRequestMessage())
                        {
                            Message.Method = HttpMethod.Post;
                            Message.RequestUri = new Uri(URL);
                            Message.Headers.TryAddWithoutValidation("content-type", ContentType);
                            Message.Headers.TryAddWithoutValidation("user-agent", UserAgent);
                            Message.Headers.TryAddWithoutValidation("referer", HttpUtility.UrlEncode(URL));

                            Message.Content = new StringContent(Data, Encoding.UTF8, "application/x-www-form-urlencoded");
                            
                            if (Cookies != null)
                            {
                                foreach (var Cookie in Cookies)
                                {
                                    try
                                    {
                                       container.Add(Cookie);
                                    }
                                    catch { }
                                }
                            }

                            using (var Response = Client.Send(Message))
                            {
                                return Response.Headers;
                            }
                        }
                    }
                    catch
                    {
                        return null;
                    }
                }
            }
        }
        protected string PostString(string URL, string ContentType, string Data, Cookie[]? Cookies = null)
        {
            lock (App.WebClient)
            {
                App.WebClient.Headers.Clear();
                App.WebClient.Headers["content-type"] = ContentType;
                App.WebClient.Headers["user-agent"] = UserAgent;
                App.WebClient.Headers["referer"] = HttpUtility.UrlEncode(URL);

                App.WebClient.Container = new CookieContainer();
                if (Cookies != null)
                {
                    foreach (var Cookie in Cookies)
                    {
                        try
                        {
                            App.WebClient.Container.Add(Cookie);
                        }
                        catch { }
                    }
                }

                return App.WebClient.UploadString(URL, Data);
            }
        }

        protected (byte[] Data, WebHeaderCollection? Headers) DownloadRequest(string URL, Cookie[]? Cookies = null)
        {
            lock (App.WebClient)
            {
                App.WebClient.Headers.Clear();
                App.WebClient.Headers["user-agent"] = UserAgent;
                App.WebClient.Headers["referer"] = HttpUtility.UrlEncode(URL);

                App.WebClient.Container = new CookieContainer();
                if (Cookies != null)
                {
                    foreach (var Cookie in Cookies)
                    {
                        try
                        {
                            App.WebClient.Container.Add(Cookie);
                        }
                        catch { }
                    }
                }

                return (App.WebClient.DownloadData(URL), App.WebClient.ResponseHeaders);
            }
        }

        public static FileHostBase[] Hosts => new FileHostBase[] {
                new Mediafire(), new GoogleDrive(), new PixelDrain(), 
                new AllDebrid(), new RealDebrid(), new OneFichier(),
                new DataNodes()
        };
    }

    public struct DownloadInfo {
        public string Url;
        public List<(string Key, string Value)> Headers;
        public Cookie[] Cookies;
        public WebProxy Proxy;
    }
}
