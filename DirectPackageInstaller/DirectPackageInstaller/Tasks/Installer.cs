using DirectPackageInstaller.Host;
using DirectPackageInstaller.IO;
using LibOrbisPkg.PKG;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using DirectPackageInstaller.Views;

namespace DirectPackageInstaller.Tasks
{
    static class Installer
    {
        public static (uint Offset, uint End, uint Size, EntryId Id)[] PKGEntries;

        public const int ServerPort = 9898;

        public static string[] CurrentFileList = null;
        public static bool AllowIndirect = false;
        public static bool ForceProxy = false;
        public static PS4Server Server;
        
        public static string EntryName = null;
        public static long EntrySize = -1;

        public static async Task<bool> PushPackage(Settings Config, Source InputType, Stream PKGStream, string URL, Action<string> SetStatus, Func<string> GetStatus, bool Silent)
        {
            if (Config.LastPS4IP == null)
            {
                await MessageBox.ShowAsync("PS4 IP not defined, please, type the PS4 IP in the Options Menu", "PS4 IP Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            StartServer(Config.LastPS4IP);

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
                InputType &= ~(Source.Proxy | Source.Segmented);

            if (InputType.HasFlag(Source.JSON))
                InputType &= ~(Source.Proxy | Source.Segmented);

            if (InputType.HasFlag(Source.File))
                InputType &= ~(Source.Proxy | Source.Segmented | Source.DiskCache);

            if (InputType.HasFlag(Source.DiskCache) || InputType.HasFlag(Source.Segmented))
                InputType &= ~Source.Proxy;

            
            uint LastResource = PKGEntries.OrderByDescending(x => x.End).First().End;

            switch (InputType)
            {
                case Source.URL | Source.SevenZip | Source.DiskCache:
                case Source.URL | Source.SevenZip:
                case Source.URL | Source.RAR | Source.DiskCache:
                case Source.URL | Source.RAR:
                    if (!Config.ProxyDownload && !AllowIndirect)
                    {
                        var Reply = await MessageBox.ShowAsync("The given pkg is compressed therefore can't be direct downloaded in your PS4.\nDo you want to the DirectPackageInstaller act as a decompress server?", "DirectPackageInstaller", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (Reply != DialogResult.Yes)
                            return false;

                        AllowIndirect = true;
                    }

                    var FreeSpace = GetCurrentDiskAvailableFreeSpace();
                    if (EntrySize > FreeSpace && !App.IsUnix)
                    {
                        long Missing = EntrySize - FreeSpace;
                        await MessageBox.ShowAsync("Compressed files are cached to your disk, you need more " + ToFileSize(Missing) + " of free space to install this package.", "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }

                    bool Retry = false;

                    var ID = DecompressService.TaskCache.Count.ToString();
                    foreach (var Task in DecompressService.TaskCache)
                    {
                        if (Task.Value.Entry == EntryName && Task.Value.Url == URL)
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
                    SetStatus("Initializing Decompressor...");

                    if (!Retry)
                    {
                        DecompressService.TaskCache[ID] = (EntryName, URL);
                        
                        string Entry = null;

                        if (InputType.HasFlag(Source.SevenZip))
                            Entry = Server.Decompress.Decompressor.CreateUn7z(URL, EntryName);
                        else 
                            Entry = Server.Decompress.Decompressor.CreateUnrar(URL, EntryName);

                        EntryName = Entry;

                        if (Entry == null)
                            throw new Exception("Failed to decompress");

                        DecompressService.EntryMap[URL] = Entry;
                    }

                    var DecompressTask = Server.Decompress.Tasks[EntryName];

                    while (DecompressTask.SafeTotalDecompressed < LastResource)
                    {
                        SetStatus($"Preloading Compressed PKG... ({(double)DecompressTask.SafeTotalDecompressed / LastResource:P})");
                        await Task.Delay(100);
                    }
                    SetStatus(OriStatus);

                    URL = $"http://{Server.IP}:{ServerPort}/{(InputType.HasFlag(Source.SevenZip) ? "un7z" : "unrar")}/?id={ID}";
                    break;

                case Source.URL | Source.DiskCache:
                case Source.URL | Source.Segmented:
                    var CacheTask = Downloader.CreateTask(URL);
                    
                    OriStatus = GetStatus();
                    while (CacheTask.SafeReadyLength < LastResource)
                    {
                        SetStatus($"Preloading PKG... ({(double)(CacheTask.SafeReadyLength) / LastResource:P})");
                        await Task.Delay(100);
                    }
                    SetStatus(OriStatus);

                    URL = $"http://{Server.IP}:{ServerPort}/cache/?b64={Convert.ToBase64String(Encoding.UTF8.GetBytes(URL))}";
                    break;

                case Source.URL | Source.Proxy:
                    URL = $"http://{Server.IP}:{ServerPort}/proxy/?b64={Convert.ToBase64String(Encoding.UTF8.GetBytes(URL))}";
                    break;

                case Source.URL | Source.JSON:
                    URL = $"http://{Server.IP}:{ServerPort}/merge/?b64={Convert.ToBase64String(Encoding.UTF8.GetBytes(URL))}";
                    break;
                case Source.File:
                    URL = $"http://{Server.IP}:{ServerPort}/file/?b64={Convert.ToBase64String(Encoding.UTF8.GetBytes(URL))}";
                    break;

                case Source.URL:
                    break;

                default:
                    MessageBox.ShowSync("Unexpected Install Method: \n" + InputType.ToString());
                    return false;
            }

            try
            {
                var Request = (HttpWebRequest)WebRequest.Create($"http://{Config.LastPS4IP}:12800/api/install");
                Request.Method = "POST";
                //Request.ContentType = "application/json";

                var EscapedURL = HttpUtility.UrlEncode(URL.Replace("https://", "http://"));
                var JSON = $"{{\"type\":\"direct\",\"packages\":[\"{EscapedURL}\"]}}";

                var Data = Encoding.UTF8.GetBytes(JSON);
                Request.ContentLength = Data.Length;

                using (Stream Stream = await Request.GetRequestStreamAsync())
                {
                    Stream.Write(Data, 0, Data.Length);
                    using (var Resp = await Request.GetResponseAsync())
                    {
                        using (var RespStream = Resp.GetResponseStream())
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

                await MessageBox.ShowAsync("Failed:\n" + Result == null ? ex.ToString() : Result, "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        public static void StartServer(string IP)
        {
            try
            {
                if (Server == null)
                {
                    var LocalIP = Locator.FindLocalIP(IP) ?? "127.0.0.1";
                    Server = new PS4Server(LocalIP, ServerPort);
                    Server.Start();
                }
            }
            catch
            {
                Server = new PS4Server("127.0.0.1", ServerPort);
                Server.Start();
            }
        }

        static string ToFileSize(double value)
        {
            string[] suffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
            for (int i = 0; i < suffixes.Length; i++)
            {
                if (value <= (Math.Pow(1024, i + 1)))
                {
                    return ThreeNonZeroDigits(value / Math.Pow(1024, i)) + " " + suffixes[i];
                }
            }

            return ThreeNonZeroDigits(value / Math.Pow(1024, suffixes.Length - 1)) + " " + suffixes[suffixes.Length - 1];
        }

        static string ThreeNonZeroDigits(double value)
        {
            if (value >= 100)
                return value.ToString("0,0");

            else if (value >= 10)
                return value.ToString("0.0");
            else
                return value.ToString("0.00");
        }

        static long GetCurrentDiskAvailableFreeSpace() => GetAvailableFreeSpace(AppDomain.CurrentDomain.BaseDirectory.Substring(0, 3));
        static long GetAvailableFreeSpace(string driveName)
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady && drive.Name.ToLowerInvariant() == driveName.ToLowerInvariant())
                {
                    return drive.AvailableFreeSpace;
                }
            }
            return -1;
        }
    }
}