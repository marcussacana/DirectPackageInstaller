using NCalc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DirectPackageInstaller.FileHosts
{
    class ZippyShare : FileHostBase
    {
        public override string HostName => "Zippyshare";

        public override DownloadInfo GetDownloadInfo(string URL)
        {
            if (!IsValidUrl(URL))
                throw new Exception("Invalid Url");

            string Page = DownloadString(URL);

            Page = Page.Substring("<a id=\"dlbutton\"  href=\"#\">");
            Page = Page.Substring("<script type=\"text/javascript\">", "</script>");

            List<(string Source, string Replace)> Replaces = new List<(string, string)>();
            while (Page.Contains("var "))
            {
                Page = Page.Substring("var ");
                var Def = Page.Substring(null, ";");
                var Name = Def.Split('=').First().Trim();
                var Value = $"({Def.Split('=').Last().Trim()})";
                Replaces.Add((Name, Value));
            }

            string Exp = Page.Substring(".href = \"").Substring("(", ")");

            foreach (var Replace in Replaces)
                Exp = Exp.Replace(Replace.Source, Replace.Replace);

            if (Page.Contains("+ \""))
                Page = Page.Substring("+ \"", "\";");
            if (Page.Contains("+\""))
                Page = Page.Substring("+\"", "\";");

            var Result = (int)new Expression(Exp).Evaluate();

            string ResultUrl = URL.Replace("file.html", $"{Result}{Page}").Replace("/v/", "/d/");
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
