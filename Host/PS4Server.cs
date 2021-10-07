using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
//using WatsonWebserver;
using HttpServerLite;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using HttpContext = HttpServerLite.HttpContext;

namespace DirectPackageInstaller.Host
{
    struct HttpRange
    {
        public HttpRange(string Header)
        {
            var RangeStr = Header.Split('=').Last();
            var BeginStr = RangeStr.Split('-').First();
            var EndStr = RangeStr.Split('-').Last();

            if (string.IsNullOrWhiteSpace(EndStr) || EndStr == "*")
                EndStr = null;

            Begin = long.Parse(BeginStr);

            if (EndStr != null)
                End = long.Parse(EndStr);
            else
                End = null;
        }

        public long Begin;
        public long? End;
        public long? Length => (End - Begin) + 1;
    }
    struct ClientInfo
    {
        public ClientInfo(PartialHttpStream HttpStream, Stream Unrar, IArchive Archive, long Errors, long LastPos)
        {
            this.HttpStream = HttpStream;
            this.Unrar = Unrar;
            this.Archive = Archive;
            this.Errors = Errors;
            this.LastPos = LastPos;
        }

        public PartialHttpStream HttpStream;
        public Stream Unrar;
        public IArchive Archive;
        public long Errors;
        public long LastPos;
    }
    public class PS4Server
    {
        public int Connections { get; private set; } = 0;

        public static Dictionary<string, (string Entry, string Url)> TaskCache = new Dictionary<string, (string Entry, string Url)>();

        Dictionary<string, int> Instances = new Dictionary<string, int>();
        Dictionary<string, string> EntryMap = new Dictionary<string, string>();
        
        Webserver Server;

        Compression Decompressor = new Compression();

        Dictionary<string, DecompressTaskInfo> Tasks => Decompressor.Tasks;

        Random Rand = new Random();

        const long MaxSkipBufferSize = 1024 * 1024 * 10;

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
            try
            {
                Connections++;
                if (Path.StartsWith("proxy"))
                    await Proxy(Context, Query);
                else if (Path.StartsWith("unrar"))
                    await Unrar(Context, Query, FromPS4);
                else
                    throw new NotImplementedException();
            }
            catch { }
            finally
            {
                Connections--;
            }

            //Context.Close();
        }

        async Task Proxy(HttpContext Context, NameValueCollection Query)
        {
            if (!Query.AllKeys.Contains("url") && !Query.AllKeys.Contains("id"))
                return;

            string Url;

            if (Query.AllKeys.Contains("url"))
                Url = Query["url"];
            else if (Query.AllKeys.Contains("id") && TaskCache.ContainsKey(Query["id"]))
                Url = TaskCache[Query["id"]].Url;
            else
                return;

            HttpRange? Range = null;
            bool Partial = Context.Request.HeaderExists("Range", true);
            if (Partial)
                Range = new HttpRange(Context.Request.Headers["Range"]);

            Context.Response.StatusCode = Partial ? 206 : 200;
            Context.Response.StatusDescription = Partial ? "Partial Content" : "OK";

            PartialHttpStream HttpStream;
            Stream Origin = HttpStream = new PartialHttpStream(Url, 1024 * 8);

            try
            {

                Context.Response.ContentLength = Origin.Length;

                Context.Response.Headers["Content-Disposition"] = $"attachment; filename=\"{((PartialHttpStream)Origin).Filename}\"";

                if (Partial)
                {
                    Context.Response.ContentLength = Range?.Length ?? Origin.Length - Range?.Begin ?? Origin.Length;
                    Context.Response.Headers["Content-Range"] = $"bytes {Range?.Begin ?? 0}-{Range?.End ?? Origin.Length}/{Origin.Length}";

                    Origin = new VirtualStream(Origin, Range?.Begin ?? 0, Context.Response.ContentLength.Value);
                }

                var Token = new CancellationTokenSource();
                await Context.Response.SendAsync(Context.Response.ContentLength.Value, Origin, Token.Token);
            }
            finally
            {
                HttpStream?.Close();
                HttpStream?.Dispose();
                Origin?.Close();
                Origin?.Dispose();
                GC.Collect();
            }
        }


        //To this shit became better we need detect when is the Remote HB that are collecting
        //info about the pkg and when is the PS4 downloading, (FromPS4 Variable in our case)
        //When the Remote Installer HB works we must allow free access to any range of the file,
        //usually he will read only a small part of the pkg then isn't a big problem wait the seek.
        //When the PS4 download We must aim to allow resume download and disallow multiple connections
        //after the already downloaded data
        async Task Unrar(HttpContext Context, NameValueCollection Query, bool FromPS4)
        {
            if (!Query.AllKeys.Contains("url") && !Query.AllKeys.Contains("id"))
                return;

            string Url = null;
            string Entry = "";

            if (Query.AllKeys.Contains("entry"))
                Entry = Query["entry"];

            if (Query.AllKeys.Contains("url"))
                Url = Query["url"];
            else if (Query.AllKeys.Contains("id") && TaskCache.ContainsKey(Query["id"]))
            {
                var Task = TaskCache[Query["id"]];
                Url = Task.Url;
                Entry = Task.Entry;
            }
            else
                return;

            HttpRange? Range = null;
            bool Partial = Context.Request.HeaderExists("Range", true);
            if (Partial)
                Range = new HttpRange(Context.Request.Headers["Range"]);

            if (!EntryMap.ContainsKey(Url) || Tasks.ContainsKey(EntryMap[Url]))
            {
                if ((Entry = await Decompressor.CreateUnrar(Url, Entry)) == null)
                    throw new NotSupportedException();

                EntryMap[Url] = Entry;
            }

            var InstanceID = Url + Entry;

            DecompressTaskInfo TaskInfo = default;
            bool SeekRequest = false;
            
            if (EntryMap.ContainsKey(Url)) {
                TaskInfo = Tasks[EntryMap[Url]];
                SeekRequest = (Range?.Begin ?? 0) > TaskInfo.SafeTotalDecompressed + MaxSkipBufferSize;
            }
            
            if (FromPS4 || !SeekRequest)
            {
                if (TaskInfo.Failed)
                    Tasks.Remove(EntryMap[Url]);

                
                if (!Instances.ContainsKey(InstanceID))
                    Instances[InstanceID] = 0;
                else if (Instances[InstanceID] > 0 && SeekRequest)
                   throw new Exception();

                if (FromPS4)
                    Instances[InstanceID]++;

                var RespData = TaskInfo.Content();

                try
                {

                    if (Partial)
                    {
                        Context.Response.ContentLength = Range?.Length ?? TaskInfo.TotalSize - Range?.Begin ?? TaskInfo.TotalSize;
                        Context.Response.Headers["Content-Range"] = $"bytes {Range?.Begin ?? 0}-{Range?.End ?? TaskInfo.TotalSize}/{TaskInfo.TotalSize}";

                        RespData = new VirtualStream(RespData, Range?.Begin ?? 0, Context.Response.ContentLength.Value);

                    }
                    else
                        RespData = new VirtualStream(RespData, 0, TaskInfo.TotalSize);

                    ((VirtualStream)RespData).ForceAmount = true;

                    await Context.Response.SendAsync(Context.Response.ContentLength ?? TaskInfo.TotalSize, RespData);
                }
                catch (Exception ex)
                {
                    throw;
                }
                finally
                {

                    RespData?.Dispose();
                    
                    if (FromPS4)
                        Instances[InstanceID]--;
                }
                return;
            }

            PartialHttpStream HttpStream = null;

            Stream Origin = null;

            (IArchive Archive, Stream Buffer, string Filename, string[] Entries, long Length) Unrar = (null, null, null, null, 0);


            Origin = HttpStream = new PartialHttpStream(Url, 1024 * 8);
            
            var Token = new CancellationTokenSource();

            try
            {
                Unrar = Main.UnrarPKG(Origin, Url, Entry, true);

                Context.Response.ContentLength = Unrar.Length;
                Context.Response.Headers["Content-Disposition"] = $"attachment; filename=\"{Unrar.Filename}\"";

                Origin = Unrar.Buffer;

                if (Partial)
                {
                    Context.Response.ContentLength = Range?.Length ?? Origin.Length - Range?.Begin ?? Origin.Length;
                    Context.Response.Headers["Content-Range"] = $"bytes {Range?.Begin ?? 0}-{Range?.End ?? Origin.Length}/{Origin.Length}";

                    Origin = new VirtualStream(Origin, Range?.Begin ?? 0, Context.Response.ContentLength.Value);
                }

                await Context.Response.SendAsync(Context.Response.ContentLength.Value, Origin, Token.Token);
                Token.Cancel();

            }
            catch (Exception ex)
            {
                throw;
            }
            finally
            {
                
                Unrar.Archive?.Dispose();
                HttpStream?.Close();
                HttpStream?.Dispose();
                Origin?.Close();
                Origin?.Dispose();
                GC.Collect();
            }
        }
    }
}
