using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DirectPackageInstaller.FileHosts
{
    class Mediafire : FileHostBase
    {
        public override DownloadInfo GetDownloadInfo(string URL)
        {
            if (!IsValidUrl(URL))
                throw new Exception("Invalid Url");

            string Page = DownloadString(URL);

            Page = Page.Substring("input popsok", "downloadButton");
            Page = Page.Substring("href=\"", "\"");

            return new DownloadInfo()
            {
                Url = Page
            };
        }

        public override bool IsValidUrl(string URL)
        {
            //https://www.mediafire.com/?550vn5tb151yyur
            //https://www.mediafire.com/file/550vn5tb151yyur/TRPViewer.exe/file
            return URL.Contains("www.mediafire.com") && (URL.Contains("/?") || URL.Contains("/file/"));
        }
    }
}
