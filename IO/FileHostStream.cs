using DirectPackageInstaller.FileHosts;

namespace DirectPackageInstaller.IO
{
    class FileHostStream : PartialHttpStream
    {
        public string PageUrl;
        public FileHostBase Host;
        public bool DirectLink { get; private set; } = true;
        public FileHostStream(string Url, int cacheLen = 8192) : base(Url, cacheLen)
        {
            foreach (var Host in FileHostBase.Hosts)
            {
                if (!Host.IsValidUrl(Url))
                    continue;

                this.Host = Host;

                PageUrl = Url;
                RefreshUrl = GetUrl;

                GetUrl();
            }
        }

        public void GetUrl()
        {
            var Info = Host.GetDownloadInfo(PageUrl);
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
                    Cookies.Add(Cookie);
                    DirectLink = false;
                }
            }
        }
    }
}
