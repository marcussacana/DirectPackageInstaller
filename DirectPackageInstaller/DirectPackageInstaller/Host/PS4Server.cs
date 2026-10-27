using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text.Json;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Web;
using DirectPackageInstaller.IO;
using DirectPackageInstaller.Tasks;
using HttpServerLite;
using static DirectPackageInstaller.SplitHelper;
using SharpCompress;
using DirectPackageInstaller.Others;
using DirectPackageInstaller.Views;

namespace DirectPackageInstaller.Host
{
    public class PS4Server
    {
        public DateTime? LastRequest { get; private set; } = null;

        const long MaxSkipBufferSize = 1024 * 1024 * 100;

        public Dictionary<string, string> JSONs = new Dictionary<string, string>();

        Dictionary<string, int> Instances = new Dictionary<string, int>();
        
        static TextWriter? LOGWRITER = null;
        static readonly object LOGLOCK = new object();
        public int Connections { get; private set; } = 0;

        /// <summary>
        /// How many requests the console made so far. Used to tell whether the console
        /// actually went for the package instead of trusting a successful socket write.
        /// </summary>
        public long TotalRequests { get; private set; } = 0;

        public static event Action<TransferProgressInfo>? GlobalTransferProgressChanged;
        public event Action<TransferProgressInfo>? TransferProgressChanged;
        public TransferProgressInfo? LastTransferProgress { get; private set; }

        public string LastRequestMode = null;
        public Webserver Server { get; private set; }

        public DecompressService Decompress = new DecompressService();

        public string IP { get => Server.Settings.Hostname; }
        public int Port { get => Server.Settings.Port; }
        public PS4Server(string IP, int Port = 9898)
        {
            Server = new Webserver(new WebserverSettings(IP, Port)
            {
                IO = new WebserverSettings.IOSettings()
                {
                    ReadTimeoutMs = TransferTuning.HttpServerReadTimeoutMs,
                    StreamBufferSize = TransferTuning.HttpServerBufferSize
                }
            });
            Decompress.ProgressReporter = ReportTransferProgress;

            //The library level messages go to the same file, they are the ones that
            //show up when a request dies without an exception reaching us.
            Server.Logger = (str) => LOG(str);

            EnableLog(App.Config.ShowError);

            Server.Routes.Default = Process;

            LOG("Server Address: {0}:{1}", IP, Port);
        }

        /// <summary>
        /// Turns the log on or off while the app is running. The file is opened on
        /// demand instead of in the constructor, otherwise turning the option on after
        /// the server is already up would have no effect.
        /// </summary>
        /// <param name="Enabled">Whether the log should be written</param>
        public static void EnableLog(bool Enabled)
        {
            lock (LOGLOCK)
            {
#if DEBUG
                Enabled = true;
#endif
                if (Enabled)
                {
                    if (LOGWRITER == null)
                        LOGWRITER = OpenLog("DPIServer.log");
                }
                else
                {
                    CloseLog();
                }
            }
        }

        /// <summary>
        /// Opens a log file, the previous session is kept next to it as .1 so the run
        /// that just failed isn't lost when the option is toggled.
        /// </summary>
        static TextWriter OpenLog(string Name)
        {
            try
            {
                //Fully qualified, File and Path are method names on this class.
                var LogPath = System.IO.Path.Combine(App.WorkingDirectory, Name);

                if (System.IO.File.Exists(LogPath))
                {
                    var Previous = LogPath + ".1";

                    try
                    {
                        if (System.IO.File.Exists(Previous))
                            System.IO.File.Delete(Previous);

                        System.IO.File.Move(LogPath, Previous);
                    }
                    catch { }
                }

                //UTF8 with a BOM, package names are often not ASCII and Notepad needs
                //the mark to detect the encoding. Create instead of CreateNew, if the
                //previous file can't be moved aside we overwrite it rather than ending
                //up with no log at all.
                return new StreamWriter(new System.IO.FileStream(LogPath, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.ReadWrite), new UTF8Encoding(true))
                {
                    AutoFlush = true
                };
            }
            catch
            {
                //No log then, better than taking the server down over it.
                return TextWriter.Null;
            }
        }

        static void CloseLog()
        {
            try
            {
                LOGWRITER?.Dispose();
            }
            catch { }

            LOGWRITER = null;
        }

        public static void LOG(string Message, params object[] Format)
        {
            lock (LOGLOCK)
            {
#if DEBUG
                if (LOGWRITER == null)
                    LOGWRITER = OpenLog("DPIServer.log");
#else
                if (LOGWRITER == null)
                    return;
#endif

                try
                {
                    LOGWRITER.WriteLine("{0} {1}", DateTime.Now.ToString("HH:mm:ss.fff"), string.Format(Message, Format));
                }
                catch { }
            }
        }

        /// <summary>
        /// Writes to the same log file from outside the server. Used to record the
        /// install stages that used to give up without saying anything.
        /// </summary>
        public static void Trace(string Message, params object[] Format) => LOG(Message, Format);

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

        private void ReportTransferProgress(TransferProgressInfo Info)
        {
            LastTransferProgress = Info;
            TransferProgressChanged?.Invoke(Info);
            GlobalTransferProgressChanged?.Invoke(Info);
        }

        private static int ConnectionID = 0;
        async Task Process(HttpContext Context)
        {
            LastRequest = DateTime.Now;
            TotalRequests++;

            int CID = ConnectionID++;

            LOG("Request '{0}' Received: {1}", CID, Context.Request.Url.Full);

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
                if (IsClientAbort(ex))
                    //The console opens the file, reads what it needs and drops the
                    //connection, that's how it probes a package. Normal, not a failure.
                    LOG("Connection '{0}' closed by the client: {1}", CID, Error(ex));
                else
                {
                    LOG("ERROR: {0}", Error(ex));
                    //Kept on its own line, the stack is identifiers and file paths so it
                    //reads the same in any language, and it's what a bug report needs.
                    LOG("STACK: {0}", ex.StackTrace);
                }
            }
            finally
            {
                LOG("Connection '{0}' Closed", CID);
                LastRequest = DateTime.Now;
                Connections--;
            }

            //Context.Close();
        }

        /// <summary>
        /// True when the other end walked away mid transfer, which the console does on
        /// purpose while it reads the package header.
        /// </summary>
        private static bool IsClientAbort(Exception ex)
        {
            for (var E = ex; E != null; E = E.InnerException)
            {
                if (E is IOException)
                    return true;

                if (E is SocketException Socket)
                    return Socket.SocketErrorCode == SocketError.ConnectionReset
                        || Socket.SocketErrorCode == SocketError.ConnectionAborted
                        || Socket.SocketErrorCode == SocketError.Shutdown;
            }

            return false;
        }

        /// <summary>
        /// Describes an exception for the log in a way that doesn't depend on the
        /// language of the machine running it. On Windows these messages come from the
        /// OS resources, so they follow the display language of the machine and stay
        /// that way even when the app itself runs in another language. The type name
        /// and the error code are always the same English identifiers, that's what
        /// makes a line searchable no matter who collected the log. The message is
        /// kept after them because it's still useful to whoever reads the log in their
        /// own language.
        /// </summary>
        public static string Error(Exception ex)
        {
            if (ex == null)
                return "no exception";

            //The innermost one names the actual cause, the outer ones are usually a
            //generic "unable to write data" style wrapper.
            var Current = ex;

            while (Current.InnerException != null)
                Current = Current.InnerException;

            //Every SocketException reports the same HResult, the useful part is the
            //socket error itself, so it's spelled out with its native number.
            if (Current is SocketException Sock)
                return Current.GetType().Name + " (" + Sock.SocketErrorCode + " / " + Sock.ErrorCode + "): " + Current.Message;

            return Current.GetType().Name + " (0x" + Current.HResult.ToString("X8") + "): " + Current.Message;
        }

        async Task File(HttpContext Context, NameValueCollection Query, string Path)
        {
            HttpRange? Range = null;
            bool Partial = Context.Request.HeaderExists("Range");
            if (Partial)
                Range = new HttpRange(Context.Request.Headers["Range"]);

            Context.Response.StatusCode = Partial ? 206 : 200;
            Context.Response.StatusDescription = Partial ? "Partial Content" : "OK";

            Stream Origin = new FileStream(
                Path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                TransferTuning.DiskBufferSize,
                TransferTuning.LocalPackageFileOptions);

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

            Stream = new VirtualStream(Stream, 0, Length) { ForceAmount = true, LeaveOpen = true };

#if DEBUG
            var dumpPath = Path.Combine(App.WorkingDirectory, "CacheDebug.bin");
            Stream = new DebugDumpStream(Stream, dumpPath);
            LOG("DEBUG: Writing sent cache data to {0} (Range: {1})", dumpPath, Range?.ToString() ?? "Full");
#endif

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
            HttpStream = new FileHostStream(Url);

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

                        Source = new FileStream(
                            File,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.Read,
                            TransferTuning.DiskBufferSize,
                            TransferTuning.LocalPackageFileOptions);
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
                long totalLength = Origin.Length;
                Context.Response.ContentLength = totalLength;

                Context.Response.Headers["Connection"] = "Keep-Alive";
                Context.Response.Headers["Accept-Ranges"] = "bytes";
                Context.Response.Headers["Content-Type"] = ContentType ?? "application/octet-stream";
                Context.Request.Keepalive = true;

                if (ContentType == null) {
                    if (Origin is FileHostStream)
                        Context.Response.Headers["Content-Disposition"] = $"attachment; filename=\"{((FileHostStream)Origin).Filename}\"";
                    else

                        Context.Response.Headers["Content-Disposition"] = $"attachment; filename=\"app.pkg\"";
                }
                
                if (Partial)
                {
                    long rangeStart = GetRangeStart(Range, totalLength);
                    long rangeLength = GetRangeLength(Range, totalLength);
                    long rangeEnd = rangeStart + rangeLength - 1;

                    if (rangeLength == 0)
                    {
                        Context.Response.StatusCode = 416;
                        Context.Response.StatusDescription = "Range Not Satisfiable";
                        Context.Response.Headers["Content-Range"] = $"bytes */{totalLength}";
                        Context.Response.Send(true);
                        return;
                    }

                    Context.Response.ContentLength = rangeLength;
                    Context.Response.Headers["Content-Range"] = $"bytes {rangeStart}-{rangeEnd}/{totalLength}";

                    Origin = new VirtualStream(Origin, rangeStart, rangeLength);
                    LOG("Serving Partial Range: {0}-{1}/{2} (Requested Length: {3})", rangeStart, rangeEnd, totalLength, rangeLength);
                }
                else 
                {
                    LOG("Serving Full Content: {0} bytes", totalLength);
                }

                LOG("Response Context: {0}", Context.Request.Url.Full);
                LOG("Content Length: {0}", Context.Response.ContentLength);

                var transferStart = Partial ? ((VirtualStream?)Origin)?.FilePos ?? 0 : 0;
                var responseLength = Context.Response.ContentLength.Value;
                var trackProgress = ContentType == null && responseLength > 0;
                var transferStarted = DateTime.Now;
                long responseSent = 0;
                object progressLock = new object();

                Origin = new BufferedStream(Origin, TransferTuning.HttpServerBufferSize);

                if (trackProgress)
                {
                    ReportTransferProgress(new TransferProgressInfo(
                        Context.Request.Url.Full,
                        transferStart,
                        totalLength,
                        0,
                        responseLength,
                        transferStarted,
                        DateTime.Now,
                        false));

                    Origin = new TransferProgressStream(Origin, read =>
                    {
                        TransferProgressInfo ProgressInfo;
                        lock (progressLock)
                        {
                            responseSent += read;
                            ProgressInfo = new TransferProgressInfo(
                                Context.Request.Url.Full,
                                transferStart + responseSent,
                                totalLength,
                                responseSent,
                                responseLength,
                                transferStarted,
                                DateTime.Now,
                                false);
                        }

                        ReportTransferProgress(ProgressInfo);
                    });
                }

                var Token = new CancellationTokenSource();
                await Context.Response.SendAsync(Context.Response.ContentLength.Value, Origin, Token.Token);

                if (trackProgress)
                {
                    ReportTransferProgress(new TransferProgressInfo(
                        Context.Request.Url.Full,
                        transferStart + responseLength,
                        totalLength,
                        responseLength,
                        responseLength,
                        transferStarted,
                        DateTime.Now,
                        true));
                }
            }
            finally
            {
                Context.Response.Close();
                Origin?.Close();
                Origin?.Dispose();
                GC.Collect();
            }
        }

        private static long GetRangeStart(HttpRange? Range, long TotalLength)
        {
            if (!Range.HasValue)
                return 0;

            return Range.Value.GetStart(TotalLength);
        }

        private static long GetRangeLength(HttpRange? Range, long TotalLength)
        {
            if (!Range.HasValue)
                return TotalLength;

            return Range.Value.GetLength(TotalLength);
        }
        
        public string RegisterJSON(string URL, string PCIP, PKGHelper.PKGInfo Info, bool AutoSplit)
        {
            try
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

                    var JSON = JsonSerializer.Serialize(Manifest, JSONContext.Default.Options);
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

                    var JSON = JsonSerializer.Serialize(Manifest, JSONContext.Default.Options);
                    JSONs.Add(ID, JSON);
                }

                return $"http://{PCIP}:{Server.Settings.Port}/json/{ID}.json";
            }
            catch (Exception ex)
            {
                MessageBox.ShowSync(Language.F("Msg.FailedRegisterJSON", ex));
                throw;
            }
        }

    }
}
