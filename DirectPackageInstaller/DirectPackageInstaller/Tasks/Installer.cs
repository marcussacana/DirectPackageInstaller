using System;
using System.IO;
using System.Net;
using System.Web;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;
using DirectPackageInstaller.Views;
using DirectPackageInstaller.Host;
using DirectPackageInstaller.IO;
using DirectPackageInstaller.Others;
using SharpCompress.Archives;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;

namespace DirectPackageInstaller.Tasks
{
    public static class Installer
    {
        public const int ServerPort = 9898;
        public static PS4Server? Server;

        public static PKGHelper.PKGInfo CurrentPKG;
        public static string[]? CurrentFileList = null;

        public static string? EntryFileName;

        private static bool ForceProxy;

        public static PayloadService Payload = new PayloadService();

        public static async Task<bool> PushPackage(Settings Config, Source InputType, Stream? PKGStream, string URL, IArchive? Decompressor, DecompressorHelperStream[]? DecompressorStreams, Func<string, Task> SetStatus, Func<string> GetStatus, bool Silent)
        {
            if (string.IsNullOrEmpty(Config.PSIP) || Config.PSIP == "0.0.0.0")
            {
                await MessageBox.ShowAsync(Language.L("Msg.PSIPNotDefined"), Language.L("Title.PSIPNotFound"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            
            if (string.IsNullOrEmpty(Config.PCIP) || Config.PCIP == "0.0.0.0")
            {
                await MessageBox.ShowAsync(Language.L("Msg.PCIPNotDefined"), Language.L("Title.PCIPNotFound"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            await StartServer(Config.PCIP);

            if (PKGStream is FileHostStream)
            {
                var HostStream = ((FileHostStream)PKGStream);
                URL = HostStream.Url;

                if (!HostStream.DirectLink && !ForceProxy)
                {
                    if (!Config.ProxyDownload)
                    {
                        var Reply = await MessageBox.ShowAsync(Language.L("Msg.NotDirectDownloadable"), "DirectPackageInstaller", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (Reply != DialogResult.Yes)
                            return false;
                    }

                    ForceProxy = true;
                }
            }

            if ((Config.ProxyDownload || ForceProxy) && !InputType.HasFlag(Source.DiskCache))
                InputType |= Source.Proxy;

            if (Config.SegmentedDownload && !InputType.HasFlag(Source.DiskCache))
                InputType |= Source.Segmented | Source.Proxy;

            
            
            //InputType is DiskCache when the file hosting is limited
            //Then segmented option must be ignored to works
            if (InputType.HasFlag(Source.DiskCache))
                InputType &= ~(Source.Segmented | Source.Proxy);


            //Just to reduce the switch cases
            if (InputType.HasFlag(Source.SevenZip) || InputType.HasFlag(Source.RAR))
                InputType &= ~(Source.Proxy | Source.Segmented | Source.DiskCache);

            if (InputType.HasFlag(Source.JSON))
                InputType &= ~(Source.Proxy | Source.DiskCache | Source.URL | Source.File);

            if (InputType.HasFlag(Source.File))
                InputType &= ~(Source.Proxy | Source.Segmented | Source.DiskCache);

            if (InputType.HasFlag(Source.DiskCache) || InputType.HasFlag(Source.Segmented))
                InputType &= ~Source.Proxy;
            
            
            if (!await MemoryInfo.EnsureFreeSpace(PKGStream, DecompressorStreams, InputType))
                return false;

            bool CanSplit = true;

            uint LastResource = CurrentPKG.PreloadLength;

            switch (InputType)
            {
                case Source.URL | Source.SevenZip:
                case Source.URL | Source.RAR:
                    CanSplit = false;

                    bool Retry = false;

                    var ID = DecompressService.TaskCache.Count.ToString();
                    foreach (var Task in DecompressService.TaskCache)
                    {
                        if (Task.Value.Entry == EntryFileName && Task.Value.Url == URL)
                        {
                            if (DecompressService.EntryMap.ContainsKey(URL) && Server.Decompress.Tasks.ContainsKey(DecompressService.EntryMap[URL]))
                            {
                                if (Server.Decompress.Tasks[DecompressService.EntryMap[URL]].Failed)
                                    continue;

                                ID = Task.Key;
                                Retry = true;
                            }
                            break;
                        }
                    }

                    var OriStatus = GetStatus();
                    await SetStatus(Language.L("Status.InitializingDecompressor"));

                    if (!Retry)
                    {
                        DecompressService.TaskCache[ID] = (EntryFileName, URL);
                        
                        string Entry = null;

                        if (await App.RunInNewThread(() => Entry = Server!.Decompress.Decompressor.CreateDecompressor(Decompressor, DecompressorStreams, EntryFileName)))
                            return false;
                        
                        EntryFileName = Entry;

                        if (Entry == null)
                            throw new AbortException("Failed to decompress");

                        DecompressService.EntryMap[URL] = Entry;
                    }

                    var DecompressTask = Server.Decompress.Tasks[EntryFileName!];

                    PS4Server.Trace("PRELOAD: waiting {0} bytes from the decompressor", LastResource);

                    while (DecompressTask.SafeTotalDecompressed < LastResource)
                    {
                        if (DecompressTask.Failed || DecompressTask.Error != null)
                        {
                            PS4Server.Trace("PRELOAD: the decompressor failed: {0}", PS4Server.Error(DecompressTask.Error) + (DecompressTask.Error == null ? ", it stopped early" : ""));
                            break;
                        }

                        await SetStatus($"Preloading Compressed PKG... ({(double)DecompressTask.SafeTotalDecompressed / LastResource:P})");
                        await Task.Delay(100);
                    }
                    await SetStatus(OriStatus);

                    URL = $"http://{Config.PCIP}:{ServerPort}/{(InputType.HasFlag(Source.SevenZip) ? "un7z" : "unrar")}/?id={ID}";
                    break;

                case Source.JSON | Source.Segmented:
                case Source.URL | Source.DiskCache:
                case Source.URL | Source.Segmented:
                    CanSplit = !InputType.HasFlag(Source.DiskCache);

                    var CacheTask = Downloader.CreateTask(URL);

                    PS4Server.Trace("PRELOAD: waiting {0} bytes of {1}", LastResource, URL);

                    OriStatus = GetStatus();
                    while (CacheTask.SafeReadyLength < LastResource)
                    {
                        if (CacheTask.Failed || CacheTask.Error != null)
                        {
                            PS4Server.Trace("PRELOAD: the download failed at {0} of {1} bytes: {2}",
                                CacheTask.SafeReadyLength, CacheTask.SafeLength, PS4Server.Error(CacheTask.Error) + (CacheTask.Error == null ? ", the server stopped sending" : ""));

                            break;
                        }

                        await SetStatus($"Preloading PKG... ({(double)(CacheTask.SafeReadyLength) / LastResource:P})");
                        await Task.Delay(100);
                    }

                    if (CacheTask.SafeReadyLength < LastResource)
                        PS4Server.Trace("PRELOAD: continuing anyway, {0} of {1} bytes are ready", CacheTask.SafeReadyLength, LastResource);

                    await SetStatus(OriStatus);

                    URL = $"http://{Config.PCIP}:{ServerPort}/cache/?b64={Convert.ToBase64String(Encoding.UTF8.GetBytes(URL))}";
                    break;

                case Source.URL | Source.Proxy:
                    URL = $"http://{Config.PCIP}:{ServerPort}/proxy/?b64={Convert.ToBase64String(Encoding.UTF8.GetBytes(URL))}";
                    break;

                case Source.JSON:
                    URL = $"http://{Config.PCIP}:{ServerPort}/merge/?b64={Convert.ToBase64String(Encoding.UTF8.GetBytes(URL))}";
                    break;
                
                case Source.File:
                    URL = $"http://{Config.PCIP}:{ServerPort}/file/?b64={Convert.ToBase64String(Encoding.UTF8.GetBytes(URL))}";
                    break;

                case Source.URL:
                    CanSplit = false;
                    break;

                default:
                    MessageBox.ShowSync(Language.F("Msg.UnexpectedInstallMethod", InputType.ToString()));
                    return false;
            }

            if (!Config.AutoSplitPKG)
                CanSplit = false;

            PS4Server.Trace("INSTALL: source {0}, url {1}, split {2}", InputType, URL, CanSplit);

            bool OK;

            string Method = "payload";

            if (await IPHelper.IsRPIOnline(Config.PSIP))
            {
                Method = "RPI";
                OK = await PushRPI(URL, Config, Silent);
            }
            else if (await IPHelper.IsEtaHenOnline(Config.PSIP))
            {
                Method = "etaHEN";
                OK = await PushEtaHen(URL, Config, Silent);
            }
            else
            {
                OK = await Payload.SendPKGPayload(Config.PSIP, Config.PCIP, URL, Silent, CanSplit);
            }

            PS4Server.Trace("INSTALL: {0} handoff {1}, sent: {2}", Method, URL, OK);

            return OK;
        }

        #region etaHEN
        public static async Task<bool> PushEtaHen(string URL, Settings Config, bool Silent)
        {
            try
            {
                string Boundary = GetBoundary();

                using var client = new HttpClient();
                var requestUri = $"http://{Config.PSIP}:12800/upload";

                var content = new MultipartFormDataContent(Boundary);
                content.Add(new StringContent("", null, "application/octet-stream"), "\"file\"", "\"\"");
                using (var buffer = new MemoryStream(Encoding.UTF8.GetBytes(URL)))
                {
                    content.Add(new StreamContent(buffer), "\"url\"");

                    var Response = await client.PostAsync(requestUri, content);

                    using var Buffer = new MemoryStream();
                    await Response.Content.CopyToAsync(Buffer);

                    var Result = Encoding.UTF8.GetString(Buffer.ToArray());

                    if (Result.Contains("SUCCESS:"))
                    {
                        if (!Silent)
                            await MessageBox.ShowAsync(Language.L("Msg.PackageSent"), "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        return true;
                    }
                    else
                    {
                        if (Result.Contains("0x80990085"))
                            Result += Language.L("Msg.VerifyFreeSpace");

                        await MessageBox.ShowAsync(Language.F("Msg.SendFailed", Result), "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                string Result = null;
                if (ex is WebException)
                {
                    try
                    {
                        using (var Resp = ((WebException)ex).Response.GetResponseStream())
                        using (MemoryStream Stream = new MemoryStream())
                        {
                            Resp.CopyTo(Stream);
                            Result = Encoding.UTF8.GetString(Stream.ToArray());
                        }
                    }
                    catch { }
                }

                await File.WriteAllTextAsync(Path.Combine(App.WorkingDirectory, "DPI-ERROR.log"), ex.ToString());
                await MessageBox.ShowAsync(Language.F("Msg.SendFailed", Result ?? ex.ToString()), "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private static string GetBoundary()
        {
            var rndData = new byte[16];
            Random rnd = new Random();
            rnd.NextBytes(rndData);
            var rndStr = string.Join("", rndData.Select(x => x.ToString("x2")));
            return "------DirectPackageInstaller_" + rndStr;
        }
        #endregion

        #region RemotePackageInstaller
        public static async Task<bool> PushRPI(string URL, Settings Config, bool Silent)
        {
            try
            {
                using var client = new HttpClient();
                var requestUri = $"http://{Config.PSIP}:12800/api/install";


                var EscapedURL = HttpUtility.UrlEncode(URL.Replace("https://", "http://"));
                var JSON = $"{{\"type\":\"direct\",\"packages\":[\"{EscapedURL}\"]}}";

                var content = new StringContent(JSON, Encoding.UTF8);
                
                var Response = await client.PostAsync(requestUri, content);

                using var Buffer = new MemoryStream();
                await Response.Content.CopyToAsync(Buffer);

                var Result = Encoding.UTF8.GetString(Buffer.ToArray());

                if (Result.Contains("\"success\""))
                {
                    if (!Silent)
                        await MessageBox.ShowAsync(Language.L("Msg.PackageSent"), "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    return true;
                }
                else
                {
                    if (Result.Contains("0x80990085"))
                        Result += Language.L("Msg.VerifyFreeSpace");

                    await MessageBox.ShowAsync(Language.F("Msg.SendFailed", Result), "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                string Result = null;
                if (ex is WebException)
                {
                    try
                    {
                        using (var Resp = ((WebException)ex).Response.GetResponseStream())
                        using (MemoryStream Stream = new MemoryStream())
                        {
                            Resp.CopyTo(Stream);
                            Result = Encoding.UTF8.GetString(Stream.ToArray());
                        }
                    }
                    catch { }
                }

                await File.WriteAllTextAsync(Path.Combine(App.WorkingDirectory, "DPI-ERROR.log"), ex.ToString());
                await MessageBox.ShowAsync(Language.F("Msg.SendFailed", Result ?? ex.ToString()), "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }
        #endregion

        #region DirectPackageInstaller
        public static async Task StartServer(string LocalIP)
        {
            if (string.IsNullOrEmpty(LocalIP))
                LocalIP = "0.0.0.0";
            
            try
            {
                if (Server == null)
                {
                    Server = new PS4Server(LocalIP, ServerPort);
                    Server.Start();
                }
            }
            catch
            {
                try
                {
                    Server = new PS4Server("0.0.0.0", ServerPort);
                    Server.Start();
                }
                catch (Exception ex)
                {
                    await MessageBox.ShowAsync(Language.F("Msg.FailedOpenHttpServer", ex), "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        #endregion

    }
}
