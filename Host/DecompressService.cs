using DirectPackageInstaller.Compression;
using HttpServerLite;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;

namespace DirectPackageInstaller.Host
{
    public class DecompressService
    {
        const long MaxSkipBufferSize = 1024 * 1024 * 10;

        Dictionary<string, int> Instances = new Dictionary<string, int>();

        public static Dictionary<string, string> EntryMap = new Dictionary<string, string>();
        public static Dictionary<string, (string Entry, string Url)> TaskCache = new Dictionary<string, (string Entry, string Url)>();

        internal SharpComp Decompressor = new SharpComp();

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

            if (!EntryMap.ContainsKey(Url) || !Tasks.ContainsKey(EntryMap[Url]))
            {
                if ((Entry = await Decompressor.CreateUnrar(Url, Entry)) == null)
                    throw new NotSupportedException();

                EntryMap[Url] = Entry;
            }

            await Decompress(Context, Url, Entry, FromPS4);
        }

        public async Task Un7z(HttpContext Context, NameValueCollection Query, bool FromPS4)
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

            if (!EntryMap.ContainsKey(Url) || !Tasks.ContainsKey(EntryMap[Url]))
            {
                if ((Entry = await Decompressor.CreateUn7z(Url, Entry)) == null)
                    throw new NotSupportedException();

                EntryMap[Url] = Entry;
            }

            await Decompress(Context, Url, Entry, FromPS4);
        }

        async Task Decompress(HttpContext Context, string Url, string Entry, bool FromPS4)
        {
            HttpRange? Range = null;
            bool Partial = Context.Request.HeaderExists("Range", true);
            if (Partial)
                Range = new HttpRange(Context.Request.Headers["Range"]);

            var InstanceID = Url + Entry;

            DecompressTaskInfo TaskInfo = default;
            bool SeekRequest = false;

            if (EntryMap.ContainsKey(Url))
            {
                TaskInfo = Tasks[EntryMap[Url]];
                SeekRequest = (Range?.Begin ?? 0) > TaskInfo.SafeTotalDecompressed + MaxSkipBufferSize;
            }

            if (TaskInfo.Failed)
            {
                if (System.Diagnostics.Debugger.IsAttached)
                    System.Diagnostics.Debugger.Break();
                System.IO.File.WriteAllText("decompress.log", $"{Tasks[EntryMap[Url]].Error}");
                Tasks.Remove(EntryMap[Url]);
            }

            if (FromPS4)
            {
                if (!Instances.ContainsKey(InstanceID))
                    Instances[InstanceID] = 0;

                Instances[InstanceID]++;
            }

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
                RespData?.Close();
                RespData?.Dispose();

                if (FromPS4)
                    Instances[InstanceID]--;
            }
        }
    }
}
