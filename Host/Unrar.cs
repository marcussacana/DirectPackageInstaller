using HttpServerLite;
using SharpCompress.Archives;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DirectPackageInstaller.Host
{
    public class UnrarService
    {
        const long MaxSkipBufferSize = 1024 * 1024 * 10;

        Dictionary<string, int> Instances = new Dictionary<string, int>();

        public static Dictionary<string, string> EntryMap = new Dictionary<string, string>();
        public static Dictionary<string, (string Entry, string Url)> TaskCache = new Dictionary<string, (string Entry, string Url)>();

        Compression Decompressor = new Compression();

        internal Dictionary<string, DecompressTaskInfo> Tasks => Decompressor.Tasks;

        public async Task Unrar(HttpContext Context, NameValueCollection Query, bool FromPS4)
        {
            if (!Query.AllKeys.Contains("url") && !Query.AllKeys.Contains("id"))
                return;

            string Url = null;
            string Entry = "";

            if (Query.AllKeys.Contains("entry"))
                Entry = Query["entry"];

            if (Query.AllKeys.Contains("url"))
                Url = Query["url"];
            
            if (Query.AllKeys.Contains("id") && TaskCache.ContainsKey(Query["id"]))
            {
                var Task = TaskCache[Query["id"]];
                Url = Task.Url;
                Entry = Task.Entry;
            }

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

            if (EntryMap.ContainsKey(Url))
            {
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
