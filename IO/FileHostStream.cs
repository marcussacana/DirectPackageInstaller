using DirectPackageInstaller.FileHosts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DirectPackageInstaller.IO
{
    class FileHostStream : PartialHttpStream
    {
        public bool DirectLink { get; private set; } = true;
        public FileHostStream(string url, int cacheLen = 8192) : base(url, cacheLen)
        {
            foreach (var Host in FileHostBase.Hosts)
            {
                if (!Host.IsValidUrl(url))
                    continue;

                var Info = Host.GetDownloadInfo(url);
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
}
