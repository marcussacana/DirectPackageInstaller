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
            if (string.IsNullOrEmpty(Config.PS4IP) || Config.PS4IP == "0.0.0.0")
            {
                await MessageBox.ShowAsync("PS4 IP not defined, please, type the PS4 IP in the Options Menu", "PS4 IP Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            
            if (string.IsNullOrEmpty(Config.PCIP) || Config.PCIP == "0.0.0.0")
            {
                await MessageBox.ShowAsync("PC IP not defined, please, type your PC LAN IP in the Options Menu", "PS4 IP Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                        var Reply = await MessageBox.ShowAsync("The given URL can't be direct downloaded.\nDo you want to the DirectPackageInstaller act as a server?", "DirectPackageInstaller", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
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
            
            
            if (!await EnsureFreeSpace(PKGStream, DecompressorStreams, InputType))
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
                    await SetStatus("Initializing Decompressor...");

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

                    while (DecompressTask.SafeTotalDecompressed < LastResource)
                    {
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
                    
                    OriStatus = GetStatus();
                    while (CacheTask.SafeReadyLength < LastResource)
                    {
                        await SetStatus($"Preloading PKG... ({(double)(CacheTask.SafeReadyLength) / LastResource:P})");
                        await Task.Delay(100);
                    }
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
                    break;

                default:
                    MessageBox.ShowSync("Unexpected Install Method: \n" + InputType.ToString());
                    return false;
            }

            bool OK;
            if (await IPHelper.IsRPIOnline(Config.PS4IP))
                OK = await PushRPI(URL, Config, Silent);
            else
                OK = await Payload.SendPKGPayload(Config.PS4IP, Config.PCIP, URL, Silent, CanSplit);
            
            return OK;
        }

        public static async Task<bool> PushRPI(string URL, Settings Config, bool Silent)
        {
            try
            {
                var Request = (HttpWebRequest)WebRequest.Create($"http://{Config.PS4IP}:12800/api/install");
                Request.Method = "POST";
                //Request.ContentType = "application/json";

                var EscapedURL = HttpUtility.UrlEncode(URL.Replace("https://", "http://"));
                var JSON = $"{{\"type\":\"direct\",\"packages\":[\"{EscapedURL}\"]}}";

                var Data = Encoding.UTF8.GetBytes(JSON);
                Request.ContentLength = Data.Length;

                await using (Stream Stream = await Request.GetRequestStreamAsync())
                {
                    await Stream.WriteAsync(Data);
                    using (var Resp = await Request.GetResponseAsync())
                    {
                        await using (var RespStream = Resp.GetResponseStream())
                        {
                            var Buffer = new MemoryStream();
                            await RespStream.CopyToAsync(Buffer);

                            var Result = Encoding.UTF8.GetString(Buffer.ToArray());

                            if (Result.Contains("\"success\""))
                            {
                                if (!Silent)
                                    await MessageBox.ShowAsync("Package Sent!", "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Information);

                                return true;
                            }
                            else
                            {
                                if (Result.Contains("0x80990085"))
                                    Result += "\nVerify if your PS4 have free space.";

                                await MessageBox.ShowAsync("Failed:\n" + Result, "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return false;
                            }
                        }
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
                await MessageBox.ShowAsync("Failed:\n" + Result == null ? ex.ToString() : Result, "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }
        
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
                    await MessageBox.ShowAsync($"Failed to Open the Http Server\n{ex}", "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static async Task<bool> EnsureFreeSpace(Stream? PKGStream, DecompressorHelperStream[]? DecompressorStreams, Source InputType)
        {
            bool AllocationRequired = InputType.HasFlag(Source.DiskCache) || InputType.HasFlag(Source.RAR)       ||
                                      InputType.HasFlag(Source.SevenZip)  || InputType.HasFlag(Source.Segmented);

            if (!AllocationRequired || PKGStream == null)
                return true;
            
            long MaxAllocationSize = PKGStream.Length;
            if (DecompressorStreams != null)
                MaxAllocationSize += DecompressorStreams.First().Length;

            long FreeSpace = 0;
            while (MaxAllocationSize > (FreeSpace = App.GetFreeStorageSpace()))
            {
                long Missing = MaxAllocationSize - FreeSpace;
                    
                var Message = $"{MaxAllocationSize.ToFileSize()} in your {(App.UseSDCard ? "SD card" : "internal storage")} is required, missing {Missing.ToFileSize()} currently.";
                
                if (App.IsAndroid)
                {
                    var CurrentStorage = App.UseSDCard;
                    App.UseSDCard = !CurrentStorage;
                    
                    var AltFreeSpace = App.GetFreeStorageSpace();
                    var AltMissingSpace = MaxAllocationSize - AltFreeSpace;
                    var AltStorageName = App.UseSDCard ? "SD card" : "internal storage";
                    
                    App.UseSDCard = CurrentStorage;
                    
                    if (AltFreeSpace > MaxAllocationSize)
                    {
                        App.UseSDCard = !CurrentStorage;
                        continue;
                    }
                    
                    Message += $"\nOr clean more {AltMissingSpace.ToFileSize()} in your {AltStorageName}.";
                }
                
                if (InputType.HasFlag(Source.Segmented))
                    Message += "\nAlternatively, you can disable Segmented Download feature.";

                if (!TempHelper.CacheIsEmpty() && Server is {Connections: 0})
                {
                    TempHelper.Clear();
                    continue;
                }
                    
                var Result = await MessageBox.ShowAsync(Message, "DirectPackageInstaller", MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning);
                if (Result != DialogResult.Retry)
                    return false;
            }

            return true;
        }

    }
}
