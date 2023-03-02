using System;
using System.IO;
using System.Linq;
using DirectPackageInstaller.Host;
using DirectPackageInstaller.Javascript;
using DirectPackageInstaller.Tasks;
using HtmlAgilityPack;
using Jint.Native;

namespace DirectPackageInstaller.FileHosts
{
    class ZippyShare : FileHostBase
    {
        public override string HostName => "Zippyshare";
        public override bool Limited => false;

        public override DownloadInfo GetDownloadInfo(string URL)
        {
            if (!IsValidUrl(URL))
                throw new Exception("Invalid Url");

            string Page = DownloadString(URL);
            
            var FullPage = new HtmlDocument();
            FullPage.LoadHtml(Page);

            var Scripts = FullPage.DocumentNode.SelectNodes("//script[@type='text/javascript' and contains(., 'dlbutton')]");

            var GetLinkJS = "\r\nif (document.getElementById('dlbutton')) this.link = document.getElementById('dlbutton').href; if (document.getElementById('fimage')) this.link = document.getElementById('fimage').href;";
            
            var Engine = JSEngine.GetEngine(FullPage);
           
            foreach (var Script in Scripts)
            {
                try
                {
                    Engine.Execute(Script.InnerHtml);
                }
                catch { }
            }

            Engine.Execute(GetLinkJS);
            
            var EvalResult = Engine.GetValue("link");

            var Result = EvalResult.AsString();

            string ResultUrl = URL.Substring(null, "/v/") + Result;
            return new DownloadInfo()
            {
                Url = ResultUrl
            };
        }

        public override bool IsValidUrl(string URL)
        {
            //https://www16.zippyshare.com/v/OPfZOy2h/file.html
            return URL.Contains(".zippyshare.com") && URL.Contains("/v/") && URL.Contains("file.html");
        }
    }
}
