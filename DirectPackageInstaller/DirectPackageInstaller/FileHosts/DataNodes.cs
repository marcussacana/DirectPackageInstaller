using System;
using System.Net;

namespace DirectPackageInstaller.FileHosts
{
    public class DataNodes : FileHostBase
    {
        public override string HostName { get; } = "DataNodes";
        public override bool Limited { get; } = false;
        public override bool IsValidUrl(string URL)
        {
            return URL.Contains("datanodes.to/") && URL.Substring("datanodes.to/").Split('/').Length == 2;
        }

        public override DownloadInfo GetDownloadInfo(string URL)
        {
            var URLParts = URL.Substring("datanodes.to/").Split('/');
            var FID = URLParts[0];
            var FNM = URLParts[1];

            var ReqData = "op=download2&id={0}&rand=&referer=https%3A%2F%2Fdatanodes.to%2Fdownload&method_free=Free+Download+%3E%3E&method_premium=&adblock_detected=";
            var Response = HeadPost("https://datanodes.to/download", "application/x-www-form-urlencoded", string.Format(ReqData, WebUtility.UrlEncode(FID)));

            if (Response == null)
                throw new Exception("Failed to get the download link");

            if (Response.Location != null)
            { 
                return new DownloadInfo()
                {
                    Url = Response.Location!.AbsoluteUri
                };
            }
            
            throw new Exception("Failed to get the download link location");
        }
    }
}