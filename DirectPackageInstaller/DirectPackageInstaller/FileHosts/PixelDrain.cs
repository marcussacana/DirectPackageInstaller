using System;
using System.Linq;

namespace DirectPackageInstaller.FileHosts
{
    class PixelDrain : FileHostBase
    {
        public override string HostName => "PixelDrain";
        public override bool Limited => false;

        public override DownloadInfo GetDownloadInfo(string URL)
        {
            if (!IsValidUrl(URL))
                throw new Exception("Invalid Url");

            string FileID = URL.Split(' ').First().Substring("/u/");

            return new DownloadInfo() {
                Url = $"https://pixeldrain.com/api/file/{FileID}?download"
            };
        }

        public override bool IsValidUrl(string URL)
        {
            return URL.Contains("pixeldrain.com/u/");
        }
    }
}

