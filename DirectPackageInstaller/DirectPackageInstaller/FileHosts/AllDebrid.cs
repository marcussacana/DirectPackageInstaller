using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace DirectPackageInstaller.FileHosts
{
    class AllDebrid : FileHostBase
    {

        static AllDebridApi? Info = null;
        static Dictionary<string, string> GenCache = new Dictionary<string, string>();

        public override string HostName => "AllDebrid";
        public override bool Limited => false;

        public override DownloadInfo GetDownloadInfo(string URL)
        {
            if (GenCache.ContainsKey(URL))
                return new DownloadInfo() { Url = GenCache[URL] };

            const string URLMask = "https://api.alldebrid.com/v4/link/unlock?agent=DirectPackageInstaller&apikey={0}&link={1}";

            var Response = DownloadString(string.Format(URLMask, App.Config.AllDebridApiKey, HttpUtility.UrlEncode(URL)));
            var Data = JsonSerializer.Deserialize<AllDebridApi>(Response);

            if (Info?.status != "success")
                throw new Exception();

            GenCache[URL] = Data.data.link;

            return new DownloadInfo()
            {
                Url = GenCache[URL] = Data.data.link
            };
        }

        public override bool IsValidUrl(string URL)
        {
            if (!App.Config.UseAllDebrid || App.Config.AllDebridApiKey.ToLowerInvariant() == "null" || string.IsNullOrEmpty(App.Config.AllDebridApiKey))
                return false;

            if (Info == null)
            {
                var Status = DownloadString("https://api.alldebrid.com/v4/user/hosts?agent=DirectPackageInstaller&apikey=" + App.Config.AllDebridApiKey);
                Info = JsonSerializer.Deserialize<AllDebridApi>(Status);
            }

            if (Info?.status != "success")
                return false;

            foreach (var Host in Info?.data.hosts) {
                if (new Regex(Host.Value.regexp).IsMatch(URL))
                    return true;
            }

            return false;
        }

        struct AllDebridApi
        {
            public string status;
            public AllDebridApiData data;
        }

        struct AllDebridApiData
        {
            public Dictionary<string, AllDebridHostsEntry> hosts;

            public string link;
            public string host;
            public string hostDomain;
            public string filename;
            public bool paws;
            public long filesize;
            public string id;
        }

        struct AllDebridHostsEntry
        {
            public string name;
            public string type;
            public string[] domains;
            public string[] regexps;
            public string regexp;
            public bool status;
        }
    }


}
