using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace DirectPackageInstaller.FileHosts
{
    class RealDebrid : FileHostBase
    {

        static string[] HostsRegex = null;
        static Dictionary<string, string> GenCache = new Dictionary<string, string>();

        public override string HostName => "RealDebrid";
        public override bool Limited => false;

        public override DownloadInfo GetDownloadInfo(string URL)
        {
            if (GenCache.ContainsKey(URL))
                return new DownloadInfo() { Url = GenCache[URL] };

            const string URLMask = "https://api.real-debrid.com/rest/1.0/unrestrict/link?auth_token={0}";

            var Response = PostString(string.Format(URLMask, App.Config.RealDebridApiKey), "application/x-www-form-urlencoded", $"link={HttpUtility.UrlEncode(URL)}");

            if (string.IsNullOrWhiteSpace(Response))
                throw new Exception();
            
            var Data = JsonSerializer.Deserialize<RealdebirdApi>(Response);


            GenCache[URL] = Data.download;

            return new DownloadInfo()
            {
                Url = GenCache[URL] = Data.download
            };
        }

        struct RealdebirdApi
        {
            public string id { get; set; }
            public string filename { get; set; }
            public string mimeType { get; set; }
            public long filesize { get; set; }
            public string link { get; set; }
            public string host { get; set; }
            public long chunks { get; set; }
            public int crc { get; set; }
            public string download { get; set; }
            public int streamable { get; set; }
        }

        public override bool IsValidUrl(string URL)
        {
            if (!App.Config.UseRealDebrid || App.Config.RealDebridApiKey.ToLowerInvariant() == "null" || string.IsNullOrEmpty(App.Config.RealDebridApiKey))
                return false;

            if (HostsRegex == null)
            {
                var Status = DownloadString("https://api.real-debrid.com/rest/1.0/hosts/regex?auth_token=" + App.Config.RealDebridApiKey);
                HostsRegex = JsonSerializer.Deserialize<string[]>(Status);
            }

            foreach (var Host in HostsRegex) {
                if (new Regex(Host, RegexOptions.None).IsMatch(URL))
                    return true;
                if (new Regex(Host.Trim('/'), RegexOptions.None).IsMatch(URL))
                    return true;
            }

            return false;
        }

    }


}
