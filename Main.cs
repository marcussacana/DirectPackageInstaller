using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;
using DirectPackageInstaller.Host;
using LibOrbisPkg.PKG;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Bmp;
using DirectPackageInstaller.IO;
using DirectPackageInstaller.Compression;

using ManagedImage = SixLabors.ImageSharp.Image;
using Image = System.Drawing.Image;
using Size = System.Drawing.Size;
using Point = System.Drawing.Point;
using System.Collections.Generic;

namespace DirectPackageInstaller
{
    public partial class Main : Form
    {
        Settings Config;

        string SettingsPath => Path.Combine(Environment.GetEnvironmentVariable("CD") ?? AppDomain.CurrentDomain.BaseDirectory, "Settings.ini");

        public Main()
        {
            InitializeComponent();

            if (Program.IsUnix)
            {

                Size TmpSize = Size;
                TmpSize.Height += 10;
                Size = TmpSize;

                tbPS4IP.AutoSize = false;
                TmpSize = tbPS4IP.Size;
                TmpSize.Width += 30;
                tbPS4IP.Size = TmpSize;

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

                MessageBox.Show($"Hello User, The focus of this tool is download PKGs from direct links but we have others minor features as well.\n\nGood to know:\nWhen using the direct download mode, you can turn off the computer or close the DirectPakcageInstaller and your PS4 will continue the download alone.\n\nWhen using the \"Proxy Downloads\" feature, the PS4 can't download the game alone and the DirectPackageInstaller must keep open.\n\nDirect PKG urls, using the \"Proxy Download\" feature or not, can be resumed anytime by just selecting 'resume' in your PS4 download list.\n\nThe DirectPackageInstaller use the port {Tasks.Installer.ServerPort} in the \"Proxy Downloads\" feature, maybe you will need to open the ports in your firewall.\n\nWhen downloading directly from compressed files, you can't resume the download after the DirectPackageInstaller is closed, but before close the DirectPackageInstaller you still can pause and resume the download in your PS4.\n\nIf your download speed is very slow, you can try enable the \"Proxy Downloads\" feature, since this feature has been created just to optimize the download speed.\n\nCreated by marcussacana", "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            miAutoDetectPS4.Checked = Config.SearchPS4;
            miProxyDownloads.Checked = Config.ProxyDownload;


            if (Config.SearchPS4 || Config.LastPS4IP == null)
            {
                Locator.OnPS4DeviceFound = (IP) => {
                    if (Config.LastPS4IP == null)
                    {
                        tbPS4IP.Text = Config.LastPS4IP = IP;
                        Tasks.Installer.StartServer(IP);
                    }
                };
            }

            if (Config.LastPS4IP == null || !Locator.IsValidPS4IP(Config.LastPS4IP))
                new Thread(() => Locator.Locate(Config.LastPS4IP == null)).Start();

            if (Config.LastPS4IP != null)
            {
                tbPS4IP.Text = Config.LastPS4IP;
                new Thread(() => Tasks.Installer.StartServer(Config.LastPS4IP)).Start();
            }

            if (Program.Updater.HaveUpdate() && MessageBox.Show(this, "Update found, Update now?", "DirectPackageInstaller", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                Program.Updater.Update();
            }
        }

        Stream PKGStream;
        
        PkgReader PKGParser;
        Pkg PKG;

        bool Fake;

        Source InputType = Source.NONE;

        private async void btnLoadUrl_Click(object sender, EventArgs e)
        {
            PKGStream?.Close();
            PKGStream?.Dispose();

            if (InputType != Source.NONE) {
                var Success = await Install(tbURL.Text, false);

                if (InputType.HasFlag(Source.File) && Success)
                    tbURL.Text = string.Empty;

                return;
            }

            if (string.IsNullOrEmpty(tbURL.Text)) {
                OpenFileDialog.ShowDialog();
                return;
            }

            PKGStream?.Close();

            bool LimitedFHost = false;

            PKG = null;
            PKGParser = null;
            PKGStream = null;

            Tasks.Installer.EntryName = null;
            Tasks.Installer.CurrentFileList = null;

            GC.Collect();

            miPackages.Visible = false;
            InputType = Source.NONE;

            if (!Uri.IsWellFormedUriString(tbURL.Text, UriKind.Absolute) && !File.Exists(tbURL.Text)) {
                MessageBox.Show(this, "Invalid URL or File Path", "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);

                return;
            }
            try
            {
                PKGStream = null;

                if (tbURL.Text.EndsWith(".json", StringComparison.InvariantCultureIgnoreCase))
                {
                    PKGStream = SplitHelper.OpenRemoteJSON(tbURL.Text);
                    InputType = Source.URL | Source.JSON;
                }
                else if (tbURL.Text.Length > 2 && (tbURL.Text[1] == ':' || tbURL.Text[0] == '/'))
                {
                    PKGStream = File.Open(tbURL.Text, FileMode.Open, FileAccess.Read, FileShare.Read);
                    InputType = Source.File;
                }
                else
                {
                    InputType = Source.URL;
                    
                    var FHStream = new FileHostStream(tbURL.Text);
                    LimitedFHost = FHStream.SingleConnection;

                    if (FHStream.SingleConnection)
                        PKGStream = new ReadSeekableStream(FHStream);
                    else
                        PKGStream = FHStream;
                }

                byte[] Magic = new byte[4];
                PKGStream.Read(Magic, 0, Magic.Length);
                PKGStream.Position = 0;

                switch (Common.DetectCompressionFormat(Magic))
                {
                    case CompressionFormat.RAR:
                        InputType |= Source.RAR;
                        SetStatus("Decompressing...");
                        var Unrar = Tasks.Decompressor.UnrarPKG(PKGStream, tbURL.Text, sender is string ? (string)sender : null);
                        PKGStream = Unrar.Buffer;
                        Tasks.Installer.EntryName = Unrar.Filename;
                        Tasks.Installer.EntrySize = Unrar.Length;
                        ListEntries(Tasks.Installer.CurrentFileList = Unrar.PKGList);
                        break;

                    case CompressionFormat.SevenZip:
                        InputType |= Source.SevenZip;
                        SetStatus("Decompressing...");
                        var Un7z = Tasks.Decompressor.Un7zPKG(PKGStream, tbURL.Text, sender is string ? (string)sender : null);
                        PKGStream = Un7z.Buffer;
                        Tasks.Installer.EntryName = Un7z.Filename;
                        Tasks.Installer.EntrySize = Un7z.Length;
                        ListEntries(Tasks.Installer.CurrentFileList = Un7z.PKGList);
                        break;
                }

                SetStatus("Reading PKG...");

                PKGParser = new PkgReader(PKGStream);
                PKG = PKGParser.ReadPkg();

                Tasks.Installer.PKGEntries = PKG.Metas.Metas.Select(x => (x.DataOffset, x.DataOffset + x.DataSize, x.DataSize, x.id)).ToArray();

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

                        using (Stream ImgBuffer = new MemoryStream(Buffer))
                        {
                            if (Program.IsUnix)
                            {
                                //I can't trust in the libgdiplus so much, so Let's make the image more simple before draw
                                using (Stream NewImgBuffer = new MemoryStream())
                                {
                                    using (var image = ManagedImage.Load(ImgBuffer))
                                    {
                                        if (image.Width > 512 || image.Height > 512)
                                            image.Mutate(x => x.Resize(512, 512));

                                        image.Save(NewImgBuffer, new BmpEncoder());
                                    }

                                    Bitmap IconBitmap = Image.FromStream(NewImgBuffer) as Bitmap;
                                    IconBox.Image = IconBitmap;
                                }
                            }
                            else
                            {   
                                Bitmap IconBitmap = Image.FromStream(ImgBuffer) as Bitmap;
                                IconBox.Image = IconBitmap;
                            }
                        }
                    }
                    catch { }
                }

                btnLoadUrl.Text = "Install";

                if (LimitedFHost)
                    MessageBox.Show("This Filehosting is limited, Even though it is compatible with DirectPackageInstaller it is not recommended for use, prefer services like alldebrid to download from this server, otherwise you may have connection and/or speed problems.", "Bad File Hosting Service", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch {
                
                IconBox.Image?.Dispose();
                IconBox.Image = null;
                ParamList.Items.Clear();

                InputType = Source.NONE;
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
                        InputType = Source.NONE;
                        btnLoadUrl_Click(Item.Text, null);
                    };

                    miPackages.DropDownItems.Insert(0, Item);
                }
            }
        }
        private async void miInstallAll_Click(object sender, EventArgs e)
        {
            if (Tasks.Installer.CurrentFileList == null)
                return;

            foreach (var File in Tasks.Installer.CurrentFileList)
            {
                Tasks.Installer.EntryName = File;
                if (!await Install(tbURL.Text, true)) {
                    var Reply = MessageBox.Show("Continue trying install the others packages?", "DirectPackageInstaller", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (Reply != DialogResult.Yes)
                        break;
                }
            }
            MessageBox.Show("Packages Sent!", "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async Task<bool> Install(string URL, bool Silent)
        {
            btnLoadUrl.Text = "Pushing...";
            miPackages.Enabled = false;
            btnLoadUrl.Enabled = false;
            tbURL.Enabled = false;
            try
            {
                tbPS4IP.Text = Config.LastPS4IP;

                if (!Locator.IsValidPS4IP(Config.LastPS4IP))
                {
                    MessageBox.Show($"Remote Package Installer Not Found at {Config.LastPS4IP}, Ensure if he is open.", "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                };

                return await Tasks.Installer.PushPackage(Config, InputType, PKGStream, URL, SetStatus, () => lblStatus.Text, Silent);
            }
            finally {
                miPackages.Enabled = true;
                btnLoadUrl.Enabled = true;
                tbURL.Enabled = true;
                btnLoadUrl.Text = "Install";
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

      

        private void SetStatus(string Status) {
            lblStatus.Text = Status;
            Application.DoEvents();
        }

        private void tbURL_TextChanged(object sender, EventArgs e)
        {
            InputType = Source.NONE;
            
            btnLoadUrl.Text = (string.IsNullOrWhiteSpace(tbURL.Text) || File.Exists(tbURL.Text)) ? "Open" : "Load";
            Tasks.Installer.CurrentFileList = null;

            miPackages.Visible = false;

            if (tbURL.Text.StartsWith("http") && !Uri.IsWellFormedUriString(tbURL.Text, UriKind.Absolute)) {
                int PathOrQueryPos = tbURL.Text.IndexOfAny(new char[] { '/', '?' }, tbURL.Text.IndexOf("://") + 3);
                var Host = tbURL.Text.Substring(0, PathOrQueryPos);

                var PathAndQuery = HttpUtility.UrlDecode(tbURL.Text.Substring(PathOrQueryPos));
                PathAndQuery = HttpUtility.UrlEncode(PathAndQuery);

                Dictionary<string, string> Replaces = new Dictionary<string, string>()
                { { "%2f", "/" }, { "%26", "&" }, { "%3f", "?" }, { "%3d", "=" }, { "+", "%20" } };

                foreach (var Pair in Replaces)
                    PathAndQuery = PathAndQuery.Replace(Pair.Key, Pair.Value);

                var NewUrl = $"{Host}{PathAndQuery}";
                if (Uri.IsWellFormedUriString(NewUrl, UriKind.Absolute))
                    tbURL.Text = NewUrl;
            }

            PKGStream?.Close();
            PKGStream?.Dispose();
        }

        private void Closing(object sender, FormClosingEventArgs e)
        {
            if (Tasks.Installer.Server?.Connections > 0)
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
                Tasks.Installer.Server?.Stop();
            }
            catch { }

            Tasks.Installer.Server = new PS4Server(Locator.FindLocalIP(Config.LastPS4IP) ?? PS4IP.AskIP("What is your Local IP?"));
            Tasks.Installer.Server.Start();
        }       

        private void TbUrlKeyDown(object sender, KeyEventArgs e)
        {
            if (Program.IsUnix && e.KeyValue == 131089)
            {
                if (tbURL.SelectionLength > 0)
                    tbURL.SelectedText = Clipboard.GetText();
                else
                    tbURL.Text = Clipboard.GetText();

                e.Handled = true;
            }
        }

        private void FileDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void FileDragDrop(object sender, DragEventArgs e)
        {
            string file = ((string[])e.Data.GetData(DataFormats.FileDrop)).First();
            tbURL.Text = file;
        }

        private void OpenFileDialog_FileOk(object sender, System.ComponentModel.CancelEventArgs e)
        {
            tbURL.Text = OpenFileDialog.FileName;
        }
    }
}
