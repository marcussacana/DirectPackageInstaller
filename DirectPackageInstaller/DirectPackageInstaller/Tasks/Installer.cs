using DirectPackageInstaller.Host;
using DirectPackageInstaller.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using DirectPackageInstaller.Views;

namespace DirectPackageInstaller.Tasks
{
    static class Installer
    {
        public const int ServerPort = 9898;
        public static PS4Server? Server;

        public static PKGHelper.PKGInfo CurrentPKG;
        public static string[]? CurrentFileList = null;

        public static string? EntryName = null;
        public static long EntrySize = -1;

        private static Socket? PayloadSocket;
        private static bool ForceProxy = false;

        public static async Task<bool> PushPackage(Settings Config, Source InputType, Stream PKGStream, string URL, Action<string> SetStatus, Func<string> GetStatus, bool Silent)
        {
            if (string.IsNullOrEmpty(Config.PS4IP))
            {
                await MessageBox.ShowAsync("PS4 IP not defined, please, type the PS4 IP in the Options Menu", "PS4 IP Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            StartServer(Config.PCIP);

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

            
            uint LastResource = CurrentPKG.Entries.MaxBy(x => x.End).End;

            switch (InputType)
            {
                case Source.URL | Source.SevenZip | Source.DiskCache:
                case Source.URL | Source.SevenZip:
                case Source.URL | Source.RAR | Source.DiskCache:
                case Source.URL | Source.RAR:
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

            bool OK = false;
            if (IPHelper.IsRPIOnline(Config.PS4IP))
                OK = await PushRPI(URL, Config, Silent);
            else
                OK = await SendPKGPayload(Config.PS4IP, Config.PCIP, URL, Silent);

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
        public static async Task<bool> SendPKGPayload(string PS4IP, string PCIP, string URL, bool Silent)
        {
            if (!File.Exists("payload.bin"))
                return false;

            var Payload = File.ReadAllBytes("payload.bin");
            var Offset = Payload.IndexOf(new byte[] { 0xB4, 0xB4, 0xB4, 0xB4, 0xB4, 0xB4});
            if (Offset == -1)
                return false;

            URL = RegisterJSON(URL, PCIP);

            Socket InfoSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            
            InfoSocket.Bind(new IPEndPoint(IPAddress.Any, 0));
            InfoSocket.Listen();

            if (PayloadSocket == null || !PayloadSocket.Connected)
            {
                if (!await TryConnectSocket(PS4IP))
                    return false;
            }

            ushort LocalPort = (ushort)((IPEndPoint)InfoSocket.LocalEndPoint).Port;
            
            var IP = IPAddress.Parse(PCIP).GetAddressBytes();
            var Port = BitConverter.GetBytes(LocalPort).Reverse().ToArray();
            
            IP.CopyTo(Payload, Offset);
            Port.CopyTo(Payload, Offset + 4);

            PayloadSocket.SendBufferSize = Payload.Length;
            
            if (PayloadSocket.Send(Payload) != Payload.Length)
                return false;

            SocketAsyncEventArgs DisconnectEvent = new SocketAsyncEventArgs();
            DisconnectEvent.RemoteEndPoint = PayloadSocket.RemoteEndPoint;
            
            PayloadSocket.Disconnect(false);
            PayloadSocket.Close();
            
            var PKGInfoSocket = await InfoSocket.AcceptAsync();
            
            List<byte> PKGInfoBuffer = new List<byte>();

            var UrlData = Encoding.UTF8.GetBytes(URL);
            var NameData = Encoding.UTF8.GetBytes(CurrentPKG.FriendlyName);
            var IDData = Encoding.UTF8.GetBytes(CurrentPKG.ContentID);
            var PKGType = Encoding.UTF8.GetBytes(CurrentPKG.BGFTContentType);
            var PackageSize = BitConverter.GetBytes(CurrentPKG.PackageSize);
            var IconData = CurrentPKG.IconData;

            if (IconData == null)
                IconData = new byte[0];
            
            PKGInfoBuffer.AddRange(BitConverter.GetBytes(UrlData.Length));
            PKGInfoBuffer.AddRange(UrlData);
            PKGInfoBuffer.AddRange(BitConverter.GetBytes(NameData.Length));
            PKGInfoBuffer.AddRange(NameData);
            PKGInfoBuffer.AddRange(BitConverter.GetBytes(IDData.Length));
            PKGInfoBuffer.AddRange(IDData);
            PKGInfoBuffer.AddRange(BitConverter.GetBytes(PKGType.Length));
            PKGInfoBuffer.AddRange(PKGType);

            PKGInfoBuffer.AddRange(PackageSize);
            
            if (IconData.Length == 0)
            {
                PKGInfoBuffer.AddRange(new byte[4]);
            }
            else
            {
                PKGInfoBuffer.AddRange(BitConverter.GetBytes(IconData.Length));
                PKGInfoBuffer.AddRange(IconData);
            }

            SocketAsyncEventArgs PkgInfoEvent = new SocketAsyncEventArgs();
            PkgInfoEvent.RemoteEndPoint = PKGInfoSocket.RemoteEndPoint;
            PkgInfoEvent.SetBuffer(PKGInfoBuffer.ToArray());
            PkgInfoEvent.Completed += (sender, e) =>
            {
                PKGInfoSocket.Close();
                InfoSocket.Close();
                PayloadSocket.Close();
            };
            
            PKGInfoSocket.SendAsync(PkgInfoEvent);

            if (!Silent)
                await MessageBox.ShowAsync("Package Sent!", "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Information);

            return true;
        }
        public static void StartServer(string LocalIP)
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
                Server = new PS4Server("0.0.0.0", ServerPort);
                Server.Start();
            }
        }

        public static async Task<bool> TryConnectSocket(string IP, bool Retry = true)
        {
            int[] Ports = new int[] { 9090, 9021, 9020 };
            foreach (var Port in Ports)
            {
                PayloadSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                PayloadSocket.ReceiveTimeout = 3000;
                PayloadSocket.SendTimeout = 3000;

                try
                {
                    await PayloadSocket.ConnectAsync(new IPEndPoint(IPAddress.Parse(IP), Port));
                    break;
                } catch{}
            }

            if (!PayloadSocket.Connected && Retry)
            {
                await Task.Delay(3000);
                return await TryConnectSocket(IP, false);
            }

            return PayloadSocket.Connected;
        }

        public static string RegisterJSON(string URL, string PCIP)
        {
            var ID = Server.JSONs.Count().ToString();
            var JSON = string.Format("{{\n  \"originalFileSize\": {0},\n  \"packageDigest\": \"{1}\",\n  \"numberOfSplitFiles\": 1,\n  \"pieces\": [\n    {{\n      \"url\": \"{2}\",\n      \"fileOffset\": 0,\n      \"fileSize\": {0},\n      \"hashValue\": \"0000000000000000000000000000000000000000\"\n    }}\n  ]\n}}", CurrentPKG.PackageSize, CurrentPKG.Digest, URL);
            Server.JSONs.Add(ID, JSON);

            return $"http://{PCIP}:{ServerPort}/json/{ID}.json";
        }

        public static int IndexOf(this IEnumerable<byte> Buffer, byte[] Content)
        {
            int Offset = 0;
            int Count = Buffer.Count() - Content.Length;
            for (int i = 0, x = 0; i < Count; i++)
            {
                if (Buffer.ElementAt(i) != Content[x])
                {
                    x = 0;
                    continue;
                }

                if (x == 0)
                    Offset = i;
                
                x++;
                if (x >= Content.Length)
                    return Offset;
            }

            return -1;
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