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
        public FileHostStream(string url, int cacheLen = 8192) : base(url, cacheLen)
        {
            foreach (var Host in FileHostBase.Hosts)
            {
                if (!Host.IsValidUrl(url))
                    continue;

                var Info = Host.GetDownloadInfo(url);
                Url = Info.Url;

                Headers = Info.Headers;

                foreach (var Cookie in Info.Cookies)
                    Cookies.Add(Cookie);
            }
        }

    }
}
