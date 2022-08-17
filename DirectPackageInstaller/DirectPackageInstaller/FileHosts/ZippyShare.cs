using System;
using DirectPackageInstaller.Javascript;
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

            Page = Page.Substring("<a id=\"dlbutton\"  href=\"#\">");
            Page = Page.Substring("<script type=\"text/javascript\">", "</script>");

            Page += "\r\nif (document.getElementById('dlbutton')) this.link = document.getElementById('dlbutton').href; if (document.getElementById('fimage')) this.link = document.getElementById('fimage').href;";
            
            var Engine = JSEngine.GetEngine(FullPage);
           
            Engine.Execute(Page);
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
