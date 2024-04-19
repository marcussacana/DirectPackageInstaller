using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text.Json;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using DirectPackageInstaller.IO;
using DirectPackageInstaller.Tasks;
using HttpServerLite;
using static DirectPackageInstaller.SplitHelper;
using SharpCompress;

namespace DirectPackageInstaller.Host
{
    public class PS4Server
    {
        public DateTime? LastRequest { get; private set; } = null;

        const long MaxSkipBufferSize = 1024 * 1024 * 100;

        public Dictionary<string, string> JSONs = new Dictionary<string, string>();

        Dictionary<string, int> Instances = new Dictionary<string, int>();
        
        static TextWriter? LOGWRITER = null;
        public int Connections { get; private set; } = 0;

        public string LastRequestMode = null;
        public Webserver Server { get; private set; }

        public DecompressService Decompress = new DecompressService();

        public string IP { get => Server.Settings.Hostname; }
        public PS4Server(string IP, int Port = 9898)
        {
            Server = new Webserver(new WebserverSettings(IP, Port)
            {
                IO = new WebserverSettings.IOSettings()
                {
                    ReadTimeoutMs = 1000 * 60 * 5,
                    StreamBufferSize = 1024 * 1024 * 2
                }
            });

#if DEBUG
            if (LOGWRITER == null)
                LOGWRITER = System.IO.File.CreateText(Path.Combine(App.WorkingDirectory, "DPIServer.log"));

            Server.Logger = (str) => LOG(str);
#else
            if (App.Config.ShowError)
                LOGWRITER = System.IO.File.CreateText(Path.Combine(App.WorkingDirectory, "DPIServer.log"));
#endif

            Server.Routes.Default = Process;

            LOG("Server Address: {0}:{1}", IP, Port);
        }

        private static void LOG(string Message, params object[] Format)
        {
#if !DEBUG
            if (!App.Config.ShowError || LOGWRITER == null)
                return;
#endif
            
            lock (LOGWRITER)
            {
                LOGWRITER.WriteLine(Message, Format);
                LOGWRITER.Flush();
            }
            
        }

        public void Start()
        {
            Server.Start();
            LOG("Server Started");
        }

        public void Stop()
        {
            try
            {
                Server.Stop();
            }
            catch { }
            LOG("Server Stopped");
        }

        private static int ConnectionID = 0;
        async Task Process(HttpContext Context)
        {
            LastRequest = DateTime.Now;

            int CID = ConnectionID++;

            LOG("Request '{0}' Received: {1}", CID, Context.Request.Url.Full);;

            bool FromPS4 = false;
            var Path = Context.Request.Url.Full;
            
            string QueryStr = "";
            if (Path.Contains("?"))
            {
                QueryStr = Path.Substring(Path.IndexOf('?') + 1);
                if (QueryStr.Contains("?"))
                {
                    QueryStr = QueryStr.Substring(0, QueryStr.IndexOf('?'));
                    FromPS4 = true;
                }
            }

            LOG("Request Client Identified as {0}", FromPS4 ? "Shell App Downloader" : "RemotePackageInstaller");

            var Query = HttpUtility.ParseQueryString(QueryStr);

            if (Path.Contains("?"))
                Path = Path.Substring(0, Path.IndexOf('?'));

            Path = Path.Trim('/');

            foreach (var Param in Query.AllKeys)
                LOG("Query Param: {0}={1}", Param, Query[Param]);

            string Url = null;

            if (Query.AllKeys.Contains("url"))
                Url = Query["url"];
            else if (Query.AllKeys.Contains("b64"))
                Url = Encoding.UTF8.GetString(Convert.FromBase64String(Query["b64"]));

            try
            {
                Connections++;

                LastRequestMode = Path.Split('\\', '/', '?').First().ToLowerInvariant();

                if (Path.StartsWith("unrar"))
                    await Decompress.Unrar(Context, Query, FromPS4);
                else if (Path.StartsWith("un7z"))
                    await Decompress.Un7z(Context, Query, FromPS4);
                else if (Path.StartsWith("json"))
                    await Json(Context, Query, Path);
                else if (Url == null)
                    throw new Exception("Missing Download Url");

                if (Path.StartsWith("proxy"))
                    await Proxy(Context, Query, Url);
                else if (Path.StartsWith("merge"))
                    await Merge(Context, Query, Url);
                else if (Path.StartsWith("split"))
                    await Split(Context, Query, Url);
                else if (Path.StartsWith("file"))
                    await File(Context, Query, Url);
                else if (Path.StartsWith("cache"))
                    await Cache(Context, Query, Url, FromPS4);
            }
            catch (Exception ex)
            {
                LOG("ERROR: {0}", ex);
            }
            finally
            {
                LOG("Connection '{0}' Closed", CID);
                LastRequest = DateTime.Now;
                Connections--;
            }

            //Context.Close();
        }

        async Task File(HttpContext Context, NameValueCollection Query, string Path)
        {
            HttpRange? Range = null;
            bool Partial = Context.Request.HeaderExists("Range");
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

        async Task Cache(HttpContext Context, NameValueCollection Query, string Url, bool FromPS4)
        {
            HttpRange? Range = null;
            bool Partial = Context.Request.HeaderExists("Range");
            if (Partial)
                Range = new HttpRange(Context.Request.Headers["Range"]);

            Context.Response.StatusCode = Partial ? 206 : 200;
            Context.Response.StatusDescription = Partial ? "Partial Content" : "OK";

            var Task = Downloader.CreateTask(Url);

            var Stream = Task.OpenRead();

            var Length = (Stream as SegmentedStream)?.Length ?? Task.SafeLength;

            while (Length == 0)
                await System.Threading.Tasks.Task.Delay(100);

            if (FromPS4)
            {
                if (!Instances.ContainsKey(Url))
                    Instances[Url] = 0;

                Instances[Url]++;
            }

            var SeekRequest = (Range?.Begin ?? 0) > Task.SafeReadyLength + MaxSkipBufferSize;

            if (FromPS4 && SeekRequest && Instances[Url] > 1)
            {
                try
                {

                    Context.Response.StatusCode = 429;
                    Context.Response.Headers["Connection"] = "close";
                    Context.Response.Headers["Retry-After"] = (60 * 5).ToString();

                    LOG("TOO MANY REQUESTS");
                    LOG("Response Context: {0}", Context.Request.Url.Full);
                    LOG("Content Length: {0}", Context.Response.ContentLength);

                    Context.Response.Send(true);
                }
                catch { }
                finally
                {
                    if (FromPS4)
                        Instances[Url]--;
                }
                return;
            }

            Stream = new VirtualStream(Stream, 0, Length) { ForceAmount = true };

            try
            {
                await SendStream(Context, Stream, Range);
            }
            catch
            {

            }
            finally
            {
                Stream.Close();

                if (FromPS4)
                    Instances[Url]--;
            }
        }

        async Task Proxy(HttpContext Context, NameValueCollection Query, string Url)
        {
            HttpRange? Range = null;
            bool Partial = Context.Request.HeaderExists("Range");
            if (Partial)
                Range = new HttpRange(Context.Request.Headers["Range"]);

            Context.Response.StatusCode = Partial ? 206 : 200;
            Context.Response.StatusDescription = Partial ? "Partial Content" : "OK";

            FileHostStream HttpStream;
            HttpStream = new FileHostStream(Url, 1024 * 8);

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
            bool Partial = Context.Request.HeaderExists("Range");
            if (Partial)
                Range = new HttpRange(Context.Request.Headers["Range"]);

            Context.Response.StatusCode = Partial ? 206 : 200;
            Context.Response.StatusDescription = Partial ? "Partial Content" : "OK";

            Stream Origin = Url.IsFilePath() ? OpenLocalJSON(Url) : OpenRemoteJSON(Url);

            try
            {
                await SendStream(Context, Origin, Range);
            }
            finally
            {
                Origin.Close();
            }
        }

        async Task Split(HttpContext Context, NameValueCollection Query, string Url)
        {
            HttpRange? Range = null;
            bool Partial = Context.Request.HeaderExists("Range");
            if (Partial)
                Range = new HttpRange(Context.Request.Headers["Range"]);


            Context.Response.StatusCode = Partial ? 206 : 200;
            Context.Response.StatusDescription = Partial ? "Partial Content" : "OK";

            Stream Source = null;

            var UriPath = Url.Substring($":{Installer.ServerPort}/");
            if (UriPath != null)
            {
                if (UriPath.StartsWith("file"))
                {
                    var QueryStr = UriPath.Substring("?");
                    if (QueryStr != null)
                    {
                        var SubQuery = HttpUtility.ParseQueryString(QueryStr);
                        
                        string File = null;

                        if (SubQuery.AllKeys.Contains("url"))
                            File = SubQuery["url"];
                        else if (SubQuery.AllKeys.Contains("b64"))
                            File = Encoding.UTF8.GetString(Convert.FromBase64String(SubQuery["b64"]));

                        Source = System.IO.File.Open(File, FileMode.Open, FileAccess.Read, FileShare.Read);
                    }
                }
            }

            if (Source == null)
            {
                var HttpStream = new FileHostStream(Url);

                if (HttpStream.SingleConnection)
                    Source = new ReadSeekableStream(HttpStream);
                else
                    Source = HttpStream;
            }

            var Offset = long.Parse(Query["offset"]);
            var Length = long.Parse(Query["size"]);


            var SubStream = new VirtualStream(Source, Offset, Length);

            try
            {
                await SendStream(Context, SubStream, Range);
            }
            finally
            {
                Source.Close();
                SubStream.Close();
            }
        }

        async Task Json(HttpContext Context, NameValueCollection Query, string Path)
        {
            HttpRange? Range = null;
            bool Partial = Context.Request.HeaderExists("Range");
            if (Partial)
                Range = new HttpRange(Context.Request.Headers["Range"]);

            Context.Response.StatusCode = Partial ? 206 : 200;
            Context.Response.StatusDescription = Partial ? "Partial Content" : "OK";

            var id = Path.Split('/').Last().Split('.').First();

            var Data = Encoding.UTF8.GetBytes(JSONs[id]);
            MemoryStream Stream = new MemoryStream();
            Stream.Write(Data, 0, Data.Length);
            Stream.Position = 0;

            await SendStream(Context, Stream, Range, "application/json");
        }

        async Task SendStream(HttpContext Context, Stream Origin, HttpRange? Range, string ContentType = null)
        {
            bool Partial = Range.HasValue;

            try
            {
                Context.Response.ContentLength = Origin.Length;

                Context.Response.Headers["Connection"] = "Keep-Alive";
                Context.Response.Headers["Accept-Ranges"] = "none";
                Context.Response.Headers["Content-Type"] = ContentType ?? "application/octet-stream";

                if (ContentType == null) {
                    if (Origin is FileHostStream)
                        Context.Response.Headers["Content-Disposition"] = $"attachment; filename=\"{((FileHostStream)Origin).Filename}\"";
                    else

                        Context.Response.Headers["Content-Disposition"] = $"attachment; filename=\"app.pkg\"";
                }
                
                if (Partial)
                {
                    Context.Response.ContentLength = Range?.Length ?? Origin.Length - Range?.Begin ?? Origin.Length;
                    Context.Response.Headers["Content-Range"] = $"bytes {Range?.Begin ?? 0}-{Range?.End ?? Origin.Length}/{Origin.Length}";

                    Origin = new VirtualStream(Origin, Range?.Begin ?? 0, Context.Response.ContentLength.Value);
                }

                LOG("Response Context: {0}", Context.Request.Url.Full);
                LOG("Content Length: {0}", Context.Response.ContentLength);

                Origin = new BufferedStream(Origin);

                var Token = new CancellationTokenSource();
                await Context.Response.SendAsync(Context.Response.ContentLength.Value, Origin, Token.Token);
            }
            finally
            {
                Context.Response.Close();
                Origin?.Close();
                Origin?.Dispose();
                GC.Collect();
            }
        }
        
        public string RegisterJSON(string URL, string PCIP, PKGHelper.PKGInfo Info, bool AutoSplit)
        {
            var ID = JSONs.Count().ToString();

            if (AutoSplit)
            {
                const long MaxPieceSize = 4294967296;


                PKGManifest Manifest = new PKGManifest();
                Manifest.originalFileSize = Info.PackageSize;
                Manifest.packageDigest = Info.Digest;

                long Offset = 0;
                long ReamingSize = Info.PackageSize;

                List<PkgPiece> Pieces = new List<PkgPiece>();
                while (ReamingSize > 0)
                {
                    var PieceSize = ReamingSize > MaxPieceSize ? MaxPieceSize : ReamingSize;
                    Pieces.Add(new PkgPiece()
                    {
                        fileOffset = Offset,
                        fileSize = PieceSize,
                        url = $"http://{PCIP}:{Installer.ServerPort}/split/?b64={Convert.ToBase64String(Encoding.UTF8.GetBytes(URL))}&offset={Offset}&size={PieceSize}",
                        hashValue = "0000000000000000000000000000000000000000"
                    });

                    Offset += PieceSize;
                    ReamingSize -= PieceSize;
                }

                Manifest.pieces = Pieces.ToArray();
                Manifest.numberOfSplitFiles = Manifest.pieces.Length;

                var JSON = JsonSerializer.Serialize(Manifest);
                JSONs.Add(ID, JSON);
            } 
            else
            {
                PKGManifest Manifest = new PKGManifest();
                Manifest.originalFileSize = Info.PackageSize;
                Manifest.packageDigest = Info.Digest;
                Manifest.numberOfSplitFiles = 1;
                Manifest.pieces = new PkgPiece[] { 
                    new PkgPiece()
                    {
                        fileSize = Info.PackageSize,
                        fileOffset = 0,
                        url = URL,
                        hashValue = "0000000000000000000000000000000000000000"
                    }
                };

                var JSON = JsonSerializer.Serialize(Manifest);
                JSONs.Add(ID, JSON);
            }

            return $"http://{PCIP}:{Server.Settings.Port}/json/{ID}.json";
        }

    }
}
