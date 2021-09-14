using LibOrbisPkg.PKG;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.MemoryMappedFiles;
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
        public Main()
        {
            InitializeComponent();
            new Thread(() => {
                Locator.Locate();
            }).Start();
        }

        PartialHttpStream PKGStream;
        
        PkgReader PKGParser;
        Pkg PKG;

        bool Loaded;
        bool Fake;

        private void btnLoadUrl_Click(object sender, EventArgs e)
        {
            PKGStream?.Close();
            PKGStream?.Dispose();

            if (Loaded) {
                Install(tbURL.Text);
                return;
            }

            PKG = null;
            PKGParser = null;
            PKGStream = null;

            GC.Collect();

            Loaded = false;
            if (!Uri.IsWellFormedUriString(tbURL.Text, UriKind.Absolute)) {
                MessageBox.Show("Invalid URL", "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                PKGStream = new PartialHttpStream(tbURL.Text);

                SetStatus("Reading PKG...");

                PKGParser = new PkgReader(PKGStream);
                PKG = PKGParser.ReadPkg();

                var SystemVer = PKG.ParamSfo.ParamSfo["SYSTEM_VER"].ToByteArray();
                var TitleName = Encoding.UTF8.GetString(PKG.ParamSfo.ParamSfo["TITLE"].ToByteArray()).Trim('\x0');

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

                        IconBox.Image = Image.FromStream(new MemoryStream(Buffer));
                    }
                    catch { }
                }

                Loaded = true;
                btnLoadUrl.Text = "Install";
            }
            catch {
                SetStatus("Failed to Open the PKG");
            }   
        }

        string IP = null;
        private void Install(string URL)
        {
            if (IP == null) {
                PS4IP Window;
                if (Locator.Devices.Any())
                {
                    var Device = Locator.Devices.First();
                    Window = new PS4IP(Device);
                }
                else
                    Window = new PS4IP();

                if (Window.ShowDialog() != DialogResult.OK)
                    return;

                IP = Window.IP;
            }

            if (!Locator.IsValidPS4IP(IP)) {
                MessageBox.Show("Remote Package Installer Not Found, Ensure if he is open.", "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                var Request = (HttpWebRequest)HttpWebRequest.Create($"http://{IP}:12800/api/install");
                Request.Method = "POST";
                //Request.ContentType = "application/json";

                var EscapedURL = HttpUtility.UrlEncode(URL.Replace("https://", "http://"));
                var JSON = $"{{\"type\":\"direct\",\"packages\":[\"{EscapedURL}\"]}}";

                var Data = Encoding.UTF8.GetBytes(JSON);
                Request.ContentLength = Data.Length;

                using (Stream Stream = Request.GetRequestStream())
                {
                    Stream.Write(Data, 0, Data.Length);
                    using (var Resp = Request.GetResponse())
                    {
                        using (var RespStream = Resp.GetResponseStream())
                        {
                            var Buffer = new MemoryStream();
                            RespStream.CopyTo(Buffer);

                            var Result = Encoding.UTF8.GetString(Buffer.ToArray());

                            if (Result.Contains("\"success\""))
                            {
                                MessageBox.Show("Package Sent!", "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            else
                            {
                                MessageBox.Show("Failed:\n" + Result, "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }
                }
            }
            catch (Exception ex){
                string Result = null;
                if (ex is WebException)
                {
                    try {
                        using (var Resp = ((WebException)ex).Response.GetResponseStream())
                        using (MemoryStream Stream = new MemoryStream()) {
                            Resp.CopyTo(Stream);
                            Result = Encoding.UTF8.GetString(Stream.ToArray());
                        }
                    }
                    catch { }
                }
                MessageBox.Show("Failed:\n" + Result == null ? ex.ToString() : Result   , "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            Loaded = false;
            btnLoadUrl.Text = "Load";
        }
    }
}
