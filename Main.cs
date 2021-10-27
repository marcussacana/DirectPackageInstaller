using DirectPackageInstaller.Host;
using LibOrbisPkg.PKG;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;

namespace DirectPackageInstaller
{
    public partial class Main : Form
    {

        Settings Config;

        string SettingsPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings.ini");

        static bool IsUnix => (int)Environment.OSVersion.Platform == 4 || (int)Environment.OSVersion.Platform == 6 || (int)Environment.OSVersion.Platform == 128;

        public Main()
        {
            InitializeComponent();

            if (IsUnix)
            {
                Size WinSize = Size;
                WinSize.Height += 10;

                Point tmpPoint = lblURL.Location;
                tmpPoint.Y += 10;
                lblURL.Location = tmpPoint;

                tmpPoint = tbURL.Location;
                tmpPoint.Y += 10;
                tbURL.Location = tmpPoint;

                tmpPoint = btnLoadUrl.Location;
                tmpPoint.Y += 10;
                btnLoadUrl.Location = tmpPoint;

                tmpPoint = SplitPanel.Location;
                tmpPoint.Y += 10;
                SplitPanel.Location = tmpPoint;

            }

            if (File.Exists(SettingsPath)) {
                Config = new Settings();
                var IniReader = new Ini(SettingsPath, "Settings");

                Config.LastPS4IP = IniReader.GetValue("LastPS4IP");
                Config.SearchPS4 = IniReader.GetBooleanValue("SearchPS4");
                Config.ProxyDownload = IniReader.GetBooleanValue("ProxyDownload");
            }
            else
            {
                Config = new Settings() { 
                    LastPS4IP = null,
                    SearchPS4 = true,
                    ProxyDownload = false
                };

                MessageBox.Show($"Hello User, The focus of this tool is download PKGs from direct links but we have others minor features as well.\n\nGood to know:\nWhen using the direct download mode, you can turn off the computer or close the DirectPakcageInstaller and your PS4 will continue the download alone.\n\nWhen using the \"Proxy Downloads\" feature, the PS4 can't download the game alone and the DirectPackageInstaller must keep open.\n\nDirect PKG urls, using the \"Proxy Download\" feature or not, can be resumed anytime by just selecting 'resume' in your PS4 download list.\n\nThe DirectPackageInstaller use the port {ServerPort} in the \"Proxy Downloads\" feature, maybe you will need to open the ports in your firewall.\n\nWhen downloading directly from compressed files, you can't resume the download after the DirectPackageInstaller is closed, but before close the DirectPackageInstaller you still can pause and resume the download in your PS4.\n\nIf your download speed is very slow, you can try enable the \"Proxy Downloads\" feature, since this feature has been created just to optimize the download speed.\n\nCreated by marcussacana", "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            miAutoDetectPS4.Checked = Config.SearchPS4;
            miProxyDownloads.Checked = Config.ProxyDownload;


            if (Config.SearchPS4 || Config.LastPS4IP == null)
            {
                Locator.OnPS4DeviceFound = (IP) => {
                    if (Config.LastPS4IP == null)
                    {
                        tbPS4IP.Text = Config.LastPS4IP = IP;
                        StartServer(IP);
                    }
                };
            }

            new Thread(() => Locator.Locate(Config.LastPS4IP == null)).Start();

            if (Config.LastPS4IP != null)
            {
                tbPS4IP.Text = Config.LastPS4IP;
                new Thread(() => StartServer(Config.LastPS4IP)).Start();
            }
        }

        Stream PKGStream;
        
        PkgReader PKGParser;
        Pkg PKG;

        bool Loaded;
        bool Fake;
        bool Compressed;

        string[] CurrentFileList = null;

        Random Rnd = new Random();

        private async void btnLoadUrl_Click(object sender, EventArgs e)
        {
            PKGStream?.Close();
            PKGStream?.Dispose();

            if (Loaded) {
                await Install(tbURL.Text, false);
                return;
            }

            PKGStream?.Close();

            PKG = null;
            PKGParser = null;
            PKGStream = null;
            EntryName = null;
            CurrentFileList = null;

            GC.Collect();

            miPackages.Visible = false;
            Compressed = false;
            Loaded = false;
            if (!Uri.IsWellFormedUriString(tbURL.Text, UriKind.Absolute)) {
                MessageBox.Show(this, "Invalid URL", "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                PKGStream = new PartialHttpStream(tbURL.Text);

                byte[] Magic = new byte[4];
                PKGStream.Read(Magic, 0, Magic.Length);
                PKGStream.Position = 0;

                switch (Compression.DetectCompressionFormat(Magic))
                {
                    case CompressionFormat.RAR:
                        Compressed = true;
                        SetStatus("Decompressing...");
                        var Unrar = UnrarPKG(PKGStream, tbURL.Text, sender is string ? (string)sender : null);
                        PKGStream = Unrar.Buffer;
                        EntryName = Unrar.Filename;
                        EntrySize = Unrar.Length;
                        ListEntries(CurrentFileList = Unrar.PKGList);
                        break;
                }

                SetStatus("Reading PKG...");

                PKGParser = new PkgReader(PKGStream);
                PKG = PKGParser.ReadPkg();

                var SystemVer = PKG.ParamSfo.ParamSfo.HasName("SYSTEM_VER") ? PKG.ParamSfo.ParamSfo["SYSTEM_VER"].ToByteArray() : new byte[4];
                var TitleName = Encoding.UTF8.GetString(PKG.ParamSfo.ParamSfo.HasName("TITLE") ? PKG.ParamSfo.ParamSfo["TITLE"].ToByteArray() : new byte[0]).Trim('\x0');

                Fake = PKG.CheckPasscode("00000000000000000000000000000000");
                SetStatus($"[{SystemVer[3]:X2}.{SystemVer[2]:X2} - {(Fake ? "Fake" : "Retail")}] {TitleName}");

                ParamList.Items.Clear();
                IconBox.Image?.Dispose();
                IconBox.Image = null;

                try
                {
                    foreach (var Param in PKG.ParamSfo.ParamSfo.Values)
                    {
                        var Name = Param.Name;
                        var RawValue = Param.ToByteArray();

                        bool DecimalValue = new[] { "APP_TYPE", "PARENTAL_LEVEL", "DEV_FLAG" }.Contains(Name);

                        var Value = Param.Type switch
                        {
                            LibOrbisPkg.SFO.SfoEntryType.Utf8Special => "",
                            LibOrbisPkg.SFO.SfoEntryType.Utf8 => Encoding.UTF8.GetString(RawValue).Trim('\x0'),
                            LibOrbisPkg.SFO.SfoEntryType.Integer => BitConverter.ToUInt32(RawValue, 0).ToString(DecimalValue ? "D1" : "X8"),
                            _ => throw new NotImplementedException(),
                        };

                        if (Name == "CATEGORY")
                            Value = ParseCategory(Value);

                        if (string.IsNullOrWhiteSpace(Value))   
                            continue;

                        ParamList.Items.Add(new ListViewItem(new[] { Name, Value }));
                    }
                }
                catch { }

                if (PKG.Metas.Metas.Where(entry => entry.id == EntryId.ICON0_PNG).FirstOrDefault() is MetaEntry Icon)
                {
                    try
                    {
                        PKGStream.Position = Icon.DataOffset;
                        byte[] Buffer = new byte[Icon.DataSize];
                        PKGStream.Read(Buffer, 0, Buffer.Length);

                        Bitmap IconBitmap = Image.FromStream(new MemoryStream(Buffer)) as Bitmap;
                        
                        if (IsUnix)
                        {
                            var NewBitmap = IconBitmap.Clone(new Rectangle(0, 0, IconBitmap.Width, IconBitmap.Height), System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                            IconBitmap.Dispose();
                            IconBitmap = NewBitmap;
                        }

                        IconBox.Image = IconBitmap;
                    }
                    catch { }
                }

                Loaded = true;
                btnLoadUrl.Text = "Install";
            }
            catch {
                
                IconBox.Image?.Dispose();
                IconBox.Image = null;
                ParamList.Items.Clear();

                Loaded = false;
                btnLoadUrl.Text = "Load";

                miPackages.Visible = false;

                SetStatus("Failed to Open the PKG");
            }

            PKGStream?.Close();
        }
        private void ListEntries(string[] PKGList)
        {
            if (PKGList == null)
                return;

            if (miPackages.Visible = PKGList.Length > 1)
            {
                miPackages.DropDownItems.Clear();
                miPackages.DropDownItems.Add(toolStripSeparator1);
                miPackages.DropDownItems.Add(miInstallAll);

                foreach (var Entry in PKGList.OrderByDescending(x=>x))
                {
                    var Item = new ToolStripMenuItem(Entry);
                    Item.Click += (sender, e) =>
                    {
                        Loaded = false;
                        btnLoadUrl_Click(Item.Text, null);
                    };

                    miPackages.DropDownItems.Insert(0, Item);
                }
            }
        }
        private async void miInstallAll_Click(object sender, EventArgs e)
        {
            if (CurrentFileList == null)
                return;

            foreach (var File in CurrentFileList)
            {
                EntryName = File;
                Loaded = true;
                if (!await Install(tbURL.Text, true)) {
                    var Reply = MessageBox.Show("Continue trying install the others packages?", "DirectPackageInstaller", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (Reply != DialogResult.Yes)
                        break;
                }
            }
            MessageBox.Show("Packages Sent!", "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        bool AllowIndirect = false;

        const int ServerPort = 9898;

        PS4Server Server;

        string EntryName = null;
        long EntrySize = -1;
        private async Task<bool> Install(string URL, bool Silent, int Retries = 10)
        {
            btnLoadUrl.Text = "Pushing...";
            miPackages.Enabled = false;
            btnLoadUrl.Enabled = false;
            tbURL.Enabled = false;
            try
            {
                if (Config.LastPS4IP == null)
                {
                    PS4IP Window;

                    if (Locator.Devices.Any())
                    {
                        var Device = Locator.Devices.First();
                        Window = new PS4IP(Device);
                    }
                    else
                        Window = new PS4IP();

                    if (Window.ShowDialog() != DialogResult.OK)
                        return false;

                    Config.LastPS4IP = Window.IP;
                }

                tbPS4IP.Text = Config.LastPS4IP;

                if (!Locator.IsValidPS4IP(Config.LastPS4IP))
                {
                    MessageBox.Show($"Remote Package Installer Not Found at {Config.LastPS4IP}, Ensure if he is open.", "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                };

                StartServer(Config.LastPS4IP);

                if (Compressed)
                {
                    if (!miProxyDownloads.Checked && !AllowIndirect)
                    {
                        var Reply = MessageBox.Show("The given pkg is compressed therefore can't be direct downloaded in your PS4.\nDo you want to the DirectPackageInstaller act as a decompress server?", "DirectPackageInstaller", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (Reply != DialogResult.Yes)
                            return false;

                        AllowIndirect = true;
                    }

                    var FreeSpace = GetCurrentDiskAvailableFreeSpace();
                    if (EntrySize > FreeSpace)
                    {
                        long Missing = EntrySize - FreeSpace;
                        MessageBox.Show("Compressed files are cached to your disk, you need more " + ToFileSize(Missing) + " of free space to install this package.", "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }

                    var ID = PS4Server.TaskCache.Count.ToString();
                    PS4Server.TaskCache[ID] = (EntryName, URL);

                    URL = $"http://{Server.IP}:{ServerPort}/unrar/?id={ID}";
                }
                else if (miProxyDownloads.Checked)
                {
                    URL = $"http://{Server.IP}:{ServerPort}/proxy/?url={HttpUtility.UrlEncode(URL)}";
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
                                        MessageBox.Show(this, "Package Sent!", "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                    return true;
                                }
                                else
                                {
                                    MessageBox.Show(this, "Failed:\n" + Result, "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    
                    if (Compressed && Retries > 0)
                    {
                        return await Install(URL, Silent, Retries - 1);
                    }

                    MessageBox.Show("Failed:\n" + Result == null ? ex.ToString() : Result, "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
            }
            finally {
                miPackages.Enabled = true;
                btnLoadUrl.Enabled = true;
                tbURL.Enabled = true;
                btnLoadUrl.Text = "Install";
            }
        }

        void StartServer(string IP)
        {
            try
            {
                if (Server == null)
                {
                    var LocalIP = Locator.FindLocalIP(IP);
                    Server = new PS4Server(LocalIP, ServerPort);
                    Server.Start();
                }
            }
            catch {
                Server = null;
            }
        }

        private string ParseCategory(string CatID) {
            return CatID.ToLowerInvariant().Trim() switch {
                "ac" => "Additional Content",
                "bd" => "Blu-ray Disc",
                "gc" => "Game Content",
                "gd" => "Game Digital Application",
                "gda" => "System Application",
                "gdc" => "Non-Game Big Application",
                "gdd" => "BG Application",
                "gde" => "Non-Game Mini App / Video Service Native App",
                "gdk" => "Video Service Web App",
                "gdl" => "PS Cloud Beta App",
                "gdo" => "PS2 Classic",
                "gp" => "Game Application Patch",
                "gpc" => "Non-Game Big App Patch",
                "gpd" => "BG Application Patch",
                "gpe" => "Non-Game Mini App Patch / Video Service Native App Patch",
                "gpk" => "Video Service Web App Patch",
                "gpl" => "PS Cloud Beta App Patch",
                "sd" => "Save Data",
                _ => "???"
            } + $" ({CatID})";
        }

        public static Dictionary<string, (string[] Links, string Password)> RARInfo = new Dictionary<string, (string[] Links, string Password)>();
        public static (IArchive Archive, Stream Buffer, string Filename, string[] PKGList, long Length) UnrarPKG(Stream Volume, string FirstUrl, string EntryName = null, bool Seekable = true, string Password = null) => UnrarPKG(new Stream[] { Volume }, FirstUrl, EntryName, Seekable, Password);
        public static (IArchive Archive, Stream Buffer, string Filename, string[] PKGList, long Length) UnrarPKG(Stream[] Volumes, string FirstUrl, string EntryName = null, bool Seekable = true, string Password = null)
        {
            bool Silent = EntryName != null;
            var Archive = RarArchive.Open(Volumes, new ReaderOptions() {
                Password = Password,
                DisableCheckIncomplete = true
            });

            bool Encrypted = Archive.Entries.Where(x => x.IsEncrypted).Any();

            if (Archive.IsMultipartVolume() && Volumes.Count() == 1)
            {
                Archive.Dispose();

                if (RARInfo.ContainsKey(FirstUrl))
                {
                    var List = RARInfo[FirstUrl];
                    return UnrarPKG(List.Links.Select(x => new PartialHttpStream(x)).ToArray(), FirstUrl, EntryName, Seekable, List.Password);
                }
                else
                {
                    var List = new LinkList(true, Encrypted, FirstUrl);
                    if (List.ShowDialog() != DialogResult.OK)
                        throw new Exception();

                    RARInfo[FirstUrl] = (List.Links, List.Password);

                    return UnrarPKG(List.Links.Select(x => new PartialHttpStream(x)).ToArray(), FirstUrl, EntryName, Seekable, List.Password);
                }
            } else if (Encrypted)
            {
                if (RARInfo.ContainsKey(FirstUrl))
                {
                    var List = RARInfo[FirstUrl];

                    var Volume = Volumes.Single();
                    Volume.Seek(0, SeekOrigin.Begin);

                    return UnrarPKG(Volume, FirstUrl, EntryName, Seekable, List.Password);
                }
                else
                {
                    var List = new LinkList(false, Encrypted, FirstUrl);
                    if (List.ShowDialog() != DialogResult.OK)
                        throw new Exception();

                    RARInfo[FirstUrl] = (List.Links, List.Password);

                    var Volume = Volumes.Single();
                    Volume.Seek(0, SeekOrigin.Begin);

                    return UnrarPKG(Volume, FirstUrl, EntryName, Seekable, List.Password);
                }
            }

            if (!Archive.IsComplete)
            {
                if (!Silent)
                    MessageBox.Show("Corrupted or missing RAR parts.", "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return (null, null, null, null, 0);
            }

            var RARs = Archive.Entries.Where(x => Path.GetExtension(x.Key).StartsWith(".r", StringComparison.OrdinalIgnoreCase));
            
            var PKGs = Archive.Entries.Where(x => x.Key.EndsWith(".pkg", StringComparison.OrdinalIgnoreCase));
            
            //if (RARs.Any() && !PKGs.Any())
            //{
            //    Archive = RarArchive.Open(RARs.ToList().Select(x => x.OpenEntryStream()));
            //}

            //PKGs = Archive.Entries.Where(x => x.Key.EndsWith(".pkg", StringComparison.OrdinalIgnoreCase));
            
            if (!PKGs.Any())
            {
                if (!Silent)
                    MessageBox.Show("No PKG Found in the given file" + (RARs.Any() ? "\nIt looks like this file has been redundantly compressed." : ""), "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return (null, null, null, null, 0);
            }

            if (PKGs.Count() == 1)
            {
                var Entry = PKGs.Single();
                
                var FileStream = Entry.OpenEntryStream();
                if (Seekable)
                    FileStream = new ReadSeekableStream(FileStream, TempHelper.GetTempFile(null));

                return (Archive, FileStream, Path.GetFileName(Entry.Key), new[] { Entry.Key }, Entry.Size);
            }

            var Files = PKGs.Select(x => Path.GetFileName(x.Key)).ToArray();

            if (!Silent)
            {
                var ChoiceBox = new Select(Files);
                if (ChoiceBox.ShowDialog() != DialogResult.OK)
                    return (null, null, null, Files, 0);
                EntryName = ChoiceBox.Choice;
            }
            else if (string.IsNullOrEmpty(EntryName))
                return (null, null, null, Files, 0);

            var SelectedEntry = PKGs.Where(x => Path.GetFileName(x.Key) == EntryName).Single();
            var SelectedFile = Path.GetFileName(SelectedEntry.Key);
            
            var Stream = SelectedEntry.OpenEntryStream();
            if (Seekable)
               Stream = new ReadSeekableStream(Stream, TempHelper.GetTempFile(null));
            
            return (Archive, Stream, SelectedFile, Files, SelectedEntry.Size);
        }
        private void SetStatus(string Status) {
            lblStatus.Text = Status;
            Application.DoEvents();
        }

        private void tbURL_TextChanged(object sender, EventArgs e)
        {
            Loaded = false;
            btnLoadUrl.Text = "Load";
            CurrentFileList = null;
            miPackages.Visible = false;

            PKGStream?.Dispose();
        }

        private void Closing(object sender, FormClosingEventArgs e)
        {
            if (Server?.Connections > 0)
            {
                var Reply = MessageBox.Show("The PS4 still downloading, do you really want exit?", "DirectPackageInstaller", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (Reply != DialogResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
            }

            if (IPAddress.TryParse(Config.LastPS4IP, out _)) {
                var Ini = new Ini(SettingsPath, "Settings");
                Ini.SetValue("LastPS4IP", Config.LastPS4IP);
                Ini.SetValue("SearchPS4", Config.SearchPS4 ? "true" : "false");
                Ini.SetValue("ProxyDownload", miProxyDownloads.Checked ? "true" : "false");
                Ini.Save();
            }

            TempHelper.Clear();
            Environment.Exit(0);
        }

        private void tbPS4IP_TextChanged(object sender, EventArgs e)
        {
            if (!IPAddress.TryParse(tbPS4IP.Text, out _))
                return;

            Config.LastPS4IP = tbPS4IP.Text;
        }

        private void miAutoDetectPS4_Click(object sender, EventArgs e)
        {
            Config.SearchPS4 = miAutoDetectPS4.Checked;
        }

        private void miRestartServer_Click(object sender, EventArgs e)
        {
            try
            {
                Server?.Stop();
            }
            catch { }

            Server = new PS4Server(Locator.FindLocalIP(Config.LastPS4IP));
            Server.Start();
        }

        //Since i'm lazy...
        //Stolen from: http://csharphelper.com/blog/2014/07/format-file-sizes-in-kb-mb-gb-and-so-forth-in-c/
        public static string ToFileSize(double value)
        {
            string[] suffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB"};
            for (int i = 0; i < suffixes.Length; i++)
            {
                if (value <= (Math.Pow(1024, i + 1)))
                {
                    return ThreeNonZeroDigits(value /
                        Math.Pow(1024, i)) +
                        " " + suffixes[i];
                }
            }

            return ThreeNonZeroDigits(value /
                Math.Pow(1024, suffixes.Length - 1)) +
                " " + suffixes[suffixes.Length - 1];
        }
        private static string ThreeNonZeroDigits(double value)
        {
            if (value >= 100)
                return value.ToString("0,0");

            else if (value >= 10)
                return value.ToString("0.0");
            else
                return value.ToString("0.00");
        }
        //Stolen code end

        private long GetCurrentDiskAvailableFreeSpace() => GetAvailableFreeSpace(AppDomain.CurrentDomain.BaseDirectory.Substring(0, 3));
        private long GetAvailableFreeSpace(string driveName)
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
