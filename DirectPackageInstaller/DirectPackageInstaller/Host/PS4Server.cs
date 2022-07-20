﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using DirectPackageInstaller.IO;
using DirectPackageInstaller.Tasks;
using HttpServerLite;
using HttpContext = HttpServerLite.HttpContext;

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
            
#if DEBUG
            if (LOGWRITER == null)
                LOGWRITER = System.IO.File.CreateText(Path.Combine(App.WorkingDirectory, "DPIServer.log"));
#else
            if (App.Config.ShowError)
                LOGWRITER = System.IO.File.CreateText(Path.Combine(App.WorkingDirectory, "DPIServer.log"));
#endif
            
            Server = new Webserver(new WebserverSettings(IP, Port)
            {
                IO = new WebserverSettings.IOSettings()
                {
                    ReadTimeoutMs = 1000 * 60 * 5,
                    StreamBufferSize = 1024 * 1024 * 2
                }
            });
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
            Server.Stop();
            LOG("Server Stopped");
        }

        private static int ConnectionID = 0;
        async Task Process(HttpContext Context)
        {
            LastRequest = DateTime.Now;

            int CID = ConnectionID++;

            LOG("Request '{0}' Received: {1}", CID, Context.Request.Url.Full);
            foreach (var Header in Context.Request.Headers)
                LOG("Request Header: {0}: {1}", Header.Key, Header.Value);

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
                else if (Path.StartsWith("torrent"))
                    await Torrent(Context, Query, Url);
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
        async Task Torrent(HttpContext Context, NameValueCollection Query, string Path)
        {
            HttpRange? Range = null;
            bool Partial = Context.Request.HeaderExists("Range", true);
            if (Partial)
                Range = new HttpRange(Context.Request.Headers["Range"]);

            Context.Response.StatusCode = Partial ? 206 : 200;
            Context.Response.StatusDescription = Partial ? "Partial Content" : "OK";

            string? EntryName = null;
            if (Query.AllKeys.Contains("fnb64"))
                EntryName = Encoding.UTF8.GetString(Convert.FromBase64String(Query["fnb64"]));

            Stream Origin;
            if (Path.IsFilePath())
                Origin = new TorrentStream(System.IO.File.ReadAllBytes(Path), EntryName);
            else
                Origin = new TorrentStream(Path, EntryName);

            try
            {
                await SendStream(Context, Origin, Range);
            }
            finally
            {
                Origin.Close();
            }
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

        async Task Cache(HttpContext Context, NameValueCollection Query, string Url, bool FromPS4)
        {
            HttpRange? Range = null;
            bool Partial = Context.Request.HeaderExists("Range", true);
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

                    foreach (var Header in Context.Response.Headers)
                        LOG("Header: {0}: {1}", Header.Key, Header.Value);

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
            bool Partial = Context.Request.HeaderExists("Range", true);
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
            bool Partial = Context.Request.HeaderExists("Range", true);
            if (Partial)
                Range = new HttpRange(Context.Request.Headers["Range"]);

            Context.Response.StatusCode = Partial ? 206 : 200;
            Context.Response.StatusDescription = Partial ? "Partial Content" : "OK";

            Stream Origin = Url.IsFilePath() ? SplitHelper.OpenLocalJSON(Url) : SplitHelper.OpenRemoteJSON(Url);

            try
            {
                await SendStream(Context, Origin, Range);
            }
            finally
            {
                Origin.Close();
            }
        }

        async Task Json(HttpContext Context, NameValueCollection Query, string Path)
        {
            HttpRange? Range = null;
            bool Partial = Context.Request.HeaderExists("Range", true);
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
                Context.Response.BufferSize = 1024 * 1024 * 2;
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

                foreach (var Header in Context.Response.Headers)
                    LOG("Header: {0}: {1}", Header.Key, Header.Value);

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
        
        public string RegisterJSON(string URL, string PCIP, PKGHelper.PKGInfo Info)
        {
            var ID = JSONs.Count().ToString();
            var JSON = string.Format("{{\n  \"originalFileSize\": {0},\n  \"packageDigest\": \"{1}\",\n  \"numberOfSplitFiles\": 1,\n  \"pieces\": [\n    {{\n      \"url\": \"{2}\",\n      \"fileOffset\": 0,\n      \"fileSize\": {0},\n      \"hashValue\": \"0000000000000000000000000000000000000000\"\n    }}\n  ]\n}}", Info.PackageSize, Info.Digest, URL);
            JSONs.Add(ID, JSON);

            return $"http://{PCIP}:{Server.Settings.Port}/json/{ID}.json";
        }

    }
}
