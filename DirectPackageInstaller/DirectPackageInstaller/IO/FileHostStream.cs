using DirectPackageInstaller.FileHosts;
using System.Collections.Generic;

namespace DirectPackageInstaller.IO
{
    public class FileHostStream : PartialHttpStream
    {
        static Dictionary<string, DownloadInfo> UrlCache = new Dictionary<string, DownloadInfo>();

        public string PageUrl;
        public FileHostBase? Host;
        public bool DirectLink { get; private set; } = true;
        public bool SingleConnection => Host?.Limited ?? false;

        public FileHostStream(string Url, int cacheLen = 8192) : base(Url, cacheLen)
        {
            App.HttpClient.Proxy = null;
            
            foreach (var Host in FileHostBase.Hosts)
            {
                if (!Host.IsValidUrl(Url))
                    continue;

                this.Host = Host;

                PageUrl = Url;
                this.Url = null;
                
                RefreshUrl = Refresh;
                break;
            }
        }

        void Refresh()
        {
            if (UrlCache.ContainsKey(PageUrl))
                UrlCache.Remove(PageUrl);
            GetUrl();
        }

        public void GetUrl()
        {
            DownloadInfo Info;

            if (UrlCache.ContainsKey(PageUrl))
            {
                Info = UrlCache[PageUrl];
            }
            else
            {
                Info = Host.GetDownloadInfo(PageUrl);
                UrlCache[PageUrl] = Info;
            }

            Url = Info.Url;

            if (Info.Headers != null)
            {
                Headers = Info.Headers;
                DirectLink = false;
            }

            if (Info.Cookies != null)
            {
                foreach (var Cookie in Info.Cookies)
                {
                    try
                    {
                        Cookies.Add(Cookie);
                        DirectLink = false;
                    }
                    catch { }
                }
            }

            if (Info.Proxy != null)
                Proxy = Info.Proxy;
        }
    }
}
