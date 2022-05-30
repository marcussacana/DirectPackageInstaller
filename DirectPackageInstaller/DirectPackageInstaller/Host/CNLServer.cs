using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using HttpServerLite;

namespace DirectPackageInstaller.Host;

public class CNLServer
{

    public Action<(string[] Links, string Password)> OnLinksReceived;
    public Webserver Server { get; private set; }

    public CNLServer()
    {
        Server = new Webserver(new WebserverSettings("0.0.0.0", 9666));
        Server.Routes.Default = Process;
    }

    private async Task Process(HttpContext Http)
    {
        var URI = Http.Request.Url.Full.ToLowerInvariant();

        if (URI.EndsWith("jdcheck.js") && Http.Request.Method == HttpMethod.GET)
        {
            Http.Response.StatusCode = 200;
            Http.Response.Headers["Server"] = "DirectPackageInstaller";
            
            await Http.Response.TrySendAsync(Encoding.UTF8.GetBytes("var jdownloader = true;"));
            Http.Response.Close();
            return;
        }

        if (URI.EndsWith("crossdomain.xml") && Http.Request.Method == HttpMethod.GET)
        {
            Http.Response.StatusCode = 200;
            Http.Response.Headers["Server"] = "DirectPackageInstaller";
            
            await Http.Response.TrySendAsync(Encoding.UTF8.GetBytes("<?xml version=\"1.0\" ?>\n<cross-domain-policy>\n<site-control permitted-cross-domain-policies=\"master-only\"/><allow-access-from domain=\"*\"/>\n<allow-http-request-headers-from domain=\"*\" headers=\"*\"/>\n</cross-domain-policy>"));
            Http.Response.Close();
            return;
        }
        
        if (!URI.Contains("flash/") || Http.Request.Method != HttpMethod.POST)
        {
            Http.Response.StatusCode = 400;
            Http.Response.TrySend(true);
            return;
        }

        var PostData = Http.Request.DataAsBytes;
        string POST = Encoding.UTF8.GetString(PostData);
        
        var Args = POST.Split('&')
            .Select(x =>
                new KeyValuePair<string, string>(x.Split('=')[0].ToLowerInvariant(),
                    HttpUtility.UrlDecode(x.Substring(x.IndexOf("=") + 1))))
            .ToArray();
        

        string Password = null;

        if (Args.Any(x => x.Key == "passwords"))
        {
            Password = Args.First(x => x.Key == "passwords").Value
                .Split('\r', '\n')
                .First();
        }
        
        if (URI.EndsWith("/add"))
        {
            if (Args.All(x => x.Key != "urls"))
            {
                Http.Response.StatusCode = 400;
                Http.Response.TrySend(true);
                return;
            }
            
            var URLs = Args.First(x => x.Key == "urls").Value
                .Split('\r', '\n')
                .Where(x => Uri.IsWellFormedUriString(x, UriKind.Absolute));

            OnLinksReceived?.Invoke((URLs.ToArray(), Password));

            Http.Response.StatusDescription = "No Content";
            Http.Response.StatusCode = 204;
            Http.Response.Headers["Server"] = "DirectPackageInstaller";
            Http.Response.TrySend(true);
            return;
        }

        if (URI.EndsWith("/addcrypted2"))
        {

            string KEY = "31323334353637383930393837363534";
            
            if (Args.All(x => x.Key != "crypted"))
            {
                Http.Response.StatusCode = 400;
                Http.Response.TrySend(true);
                Http.Close();
                return;
            }

            byte[] CrypteUrls = Convert.FromBase64String(Args.First(x => x.Key == "crypted").Value);

            if (Args.Any(x => x.Key == "jk"))
            {
                var Script = Args.First(x => x.Key == "jk").Value;

                if (Script.Contains("return"))
                {
                    Script = Script.Substring(Script.IndexOf("return"));
                    Script = Script.Substring(Script.IndexOf(' ')).Trim();

                    var Open = Script.First();
                    Script = Script.Substring(1);
                    Script = Script.Split(Open).First();
                }

                KEY = string.Join("", Script.Where(x => x is >= '0' and <= 'F'));
            }

            List<byte> KeyData = new List<byte>();
            for (int i = 0; i < KEY.Length; i += 2)
            {
                var Byte = Convert.ToByte(KEY.Substring(i, 2), 16);
                KeyData.Add(Byte);
            }

            try
            {
                using RijndaelManaged Aes = new RijndaelManaged()
                {
                    Key = KeyData.ToArray(),
                    Mode = CipherMode.CBC,
                    Padding = PaddingMode.None,
                    IV = KeyData.ToArray()
                };

                using var Decryptor = Aes.CreateDecryptor();

                var Data = Decryptor.TransformFinalBlock(CrypteUrls, 0, CrypteUrls.Length);

                var DataUTF8 = Encoding.UTF8.GetString(Data);

                var DecryptedUrls = DataUTF8.Split('\r', '\n', '\x0')
                    .Where(x => Uri.IsWellFormedUriString(x, UriKind.Absolute));
                
                OnLinksReceived?.Invoke((DecryptedUrls.ToArray(), Password));
            }
            catch
            {
                Http.Response.StatusCode = 400;
                Http.Response.TrySend(true);
                Http.Close();
                return;
            }

            Http.Response.StatusDescription = "No Content";
            Http.Response.StatusCode = 204;
            Http.Response.Headers["Server"] = "DirectPackageInstaller";
            Http.Response.TrySend(true);
            return;
        }
    }
}