using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using DirectPackageInstaller.IO;
//using WatsonWebserver;
using HttpServerLite;
using HttpContext = HttpServerLite.HttpContext;

namespace DirectPackageInstaller.Host
{
    public class PS4Server
    {
        public int Connections { get; private set; } = 0;
        
        Webserver Server;

        public DecompressService Decompress = new DecompressService();

        public string IP { get => Server.Settings.Hostname; }
        public PS4Server(string IP, int Port = 9898)
        {
            Server = new Webserver(new WebserverSettings(IP, Port)
            {
                IO = new WebserverSettings.IOSettings()
                {
                    ReadTimeoutMs = 1000 * 60 * 5,
                    StreamBufferSize = 1024 * 8
                }
            });
            Server.Routes.Default = Process;
        }
        public void Start()
        {
            Server.Start();
        }
        public void Stop()
        {
            Server.Stop();
        }

        async Task Process(HttpContext Context)
        {
            bool FromPS4 = false;
            var Path = Context.Request.Url.Full;
            var QueryStr = Path.Substring(Path.IndexOf('?') + 1);
            if (QueryStr.Contains("?"))
            {
                QueryStr = QueryStr.Substring(0, QueryStr.IndexOf('?'));
                FromPS4 = true;
            }

            var Query = HttpUtility.ParseQueryString(QueryStr);
            Path = Path.Substring(0, Path.IndexOf('?')).Trim('/');


            string Url = null;

            if (Query.AllKeys.Contains("url"))
                Url = Query["url"];
            else if (Query.AllKeys.Contains("b64"))
                Url = Encoding.UTF8.GetString(Convert.FromBase64String(Query["b64"]));

            try
            {
                Connections++;

                if (Path.StartsWith("unrar"))
                    await Decompress.Unrar(Context, Query, FromPS4);
                else
                if (Path.StartsWith("un7z"))
                    await Decompress.Un7z(Context, Query, FromPS4);
                else if (Url == null)
                    throw new Exception("Missing Download Url");

                if (Path.StartsWith("proxy"))
                    await Proxy(Context, Query, Url);
                else if (Path.StartsWith("merge"))
                    await Merge(Context, Query, Url);
                else if (Path.StartsWith("file"))
                    await File(Context, Query, Url);
            }
            catch { }
            finally
            {
                Connections--;
            }

            //Context.Close();
        }

        async Task File(HttpContext Context, NameValueCollection Query, string Path)
        {
            HttpRange? Range = null;
            bool Partial = Context.Request.HeaderExists("Range", true);
            if (Partial)
                Range = new HttpRange(Context.Request.Headers["Range"]);

            Context.Response.StatusCode = Partial ? 206 : 200;
            Context.Response.StatusDescription = Partial ? "Partial Content" : "OK";

            Stream Origin = System.IO.File.Open(Path, FileMode.Open, FileAccess.Read, FileShare.Read);

            try
            {
                await SendStream(Context, Origin, Range);
            }
            finally
            {
                Origin.Close();
            }
        }

        async Task Proxy(HttpContext Context, NameValueCollection Query, string Url)
        {
            HttpRange? Range = null;
            bool Partial = Context.Request.HeaderExists("Range", true);
            if (Partial)
                Range = new HttpRange(Context.Request.Headers["Range"]);

            Context.Response.StatusCode = Partial ? 206 : 200;
            Context.Response.StatusDescription = Partial ? "Partial Content" : "OK";

            FileHostStream HttpStream;
            HttpStream = new FileHostStream(Url, 1024 * 8);
            HttpStream.TryBypassProxy = true;
                

            try
            {
                await SendStream(Context, HttpStream.SingleConnection ? (Stream) new ReadSeekableStream(HttpStream) : HttpStream, Range);
            }
            finally {
                HttpStream.Close();
            }
        }

        async Task Merge(HttpContext Context, NameValueCollection Query, string Url) {

            HttpRange? Range = null;
            bool Partial = Context.Request.HeaderExists("Range", true);
            if (Partial)
                Range = new HttpRange(Context.Request.Headers["Range"]);

            Context.Response.StatusCode = Partial ? 206 : 200;
            Context.Response.StatusDescription = Partial ? "Partial Content" : "OK";

            Stream Origin = SplitHelper.OpenRemoteJSON(Url);

            try
            {
                await SendStream(Context, Origin, Range);
            }
            finally
            {
                Origin.Close();
            }
        }

        async Task SendStream(HttpContext Context, Stream Origin, HttpRange? Range)
        {
            bool Partial = Range.HasValue;

            try
            {

                Context.Response.ContentLength = Origin.Length;

                if (Origin is FileHostStream)
                    Context.Response.Headers["Content-Disposition"] = $"attachment; filename=\"{((FileHostStream)Origin).Filename}\"";
                else

                    Context.Response.Headers["Content-Disposition"] = $"attachment; filename=\"app.pkg\"";

                if (Partial)
                {
                    Context.Response.ContentLength = Range?.Length ?? Origin.Length - Range?.Begin ?? Origin.Length;
                    Context.Response.Headers["Content-Range"] = $"bytes {Range?.Begin ?? 0}-{Range?.End ?? Origin.Length}/{Origin.Length}";

                    Origin = new VirtualStream(Origin, Range?.Begin ?? 0, Context.Response.ContentLength.Value);
                }

                Origin = new BufferedStream(Origin);

                var Token = new CancellationTokenSource();
                await Context.Response.SendAsync(Context.Response.ContentLength.Value, Origin, Token.Token);
            }
            finally
            {
                Origin?.Close();
                Origin?.Dispose();
                GC.Collect();
            }
        }
    }
}
