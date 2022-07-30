using NCalc;
using System;
using System.Collections.Generic;
using System.Linq;
using HtmlAgilityPack;

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

            List<(string Source, string Replace)> Replaces = new List<(string, string)>();
            while (Page.Contains("var "))
            {
                Page = Page.Substring("var ");
                var Def = Page.Substring(null, ";");
                string Name = Def.Split('=').First().Trim();
                
                Page = Page.Substring("=");//skip

                string Value;
                if (Def.Contains("function"))
                    Value = Def.Substring("return").Split('}', ';').First().Trim();
                else 
                    Value = $"({Def.Split('=').Last().Trim()})";

                HtmlNode Node = null;
                if (Value.Contains("getElementById"))
                {
                    var ElmId = Value.TrimStart('(').Substring("(", ")").Trim('"', '\'');
                    Node = FullPage.DocumentNode.SelectSingleNode($"//*[@id='{ElmId}']");
                }

                if (Value.Contains("getAttribute"))
                {
                    Value = Value.Substring("getAttribute");
                    var AttribName = Value.Substring("(", ")").Trim('"', '\'');
                    Value = Node.GetAttributeValue(AttribName, null);
                }

                if (Page.Contains($"{Name} =") || Page.Contains($"{Name}="))
                {
                    var SubExp = Page;
                    if (Page.Contains($"{Name} ="))
                        SubExp = Page.Substring($"{Name} =");
                    else if (Page.Contains($"{Name}="))
                        SubExp = Page.Substring($"{Name}=");

                    SubExp = SubExp.Split(';', '}').First();
                    foreach (var Replace in Replaces)
                        SubExp = SubExp.Replace(Replace.Source, Replace.Replace);

                    SubExp = SubExp.Replace(Name, Value);
                    
                    Value = ((int)new Expression(SubExp).Evaluate()).ToString();
                }

                foreach (var Replace in Replaces)
                    Value = Value.Replace(Replace.Source, Replace.Replace);
                
                Replaces.Add(($"{Name}()", Value));
                Replaces.Add((Name, Value));
            }

            string Exp = Page.Substring(".href = \"").Substring("+(", ")+");

            foreach (var Replace in Replaces)
                Exp = Exp.Replace(Replace.Source, Replace.Replace);

            if (Page.Contains("+ \""))
                Page = Page.Substring("+ \"", "\";");
            if (Page.Contains("+\""))
                Page = Page.Substring("+\"", "\";");

            var Result = new Expression(Exp).Evaluate();

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
