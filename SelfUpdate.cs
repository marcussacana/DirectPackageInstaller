using Ionic.Zip;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DirectPackageInstaller
{
    class SelfUpdate
    {
        public static string MainExecutable = new Uri(System.Reflection.Assembly.GetEntryAssembly().CodeBase).LocalPath;
        public static string TempUpdateDir = Path.GetDirectoryName(MainExecutable) + "\\LastVersion\\";

        string Repo = "https://raw.githubusercontent.com/marcussacana/DirectPackageInstaller/Updater/";

        const string UpdateList = "Update.ini";

        Version CurrentVersion = new Version("5.3.0");

        string Runtime;

        public SelfUpdate()
        {
            string OS = RuntimeInformation.OSDescription;

            if (OS.ToLowerInvariant().Contains("windows"))
                OS = "Windows";
            else if (OS.ToLowerInvariant().Contains("linux"))
                OS = "Linux";
            else if (OS.ToLowerInvariant().Contains("darwin"))
                OS = "OSX";
            else
                throw new PlatformNotSupportedException();

            string Arch;
            switch (RuntimeInformation.ProcessArchitecture)
            {
                case Architecture.Arm:
                    Arch = "ARM";
                    break;
                case Architecture.Arm64:
                    Arch = "ARM64";
                    break;
                case Architecture.X86:
                    Arch = "X86";
                    break;
                case Architecture.X64:
                    Arch = "X64";
                    break;
                default:
                    throw new PlatformNotSupportedException();
            }

            Runtime = $"{OS}-{Arch}";

            GitHub.BypassSLL();
        }


        public bool HasUpdates()
        {
            try
            {
                string UpdateInfo = DownloadString(Repo + UpdateList);
                Ini IniReader = new Ini(UpdateInfo.Replace("\r\n", "\n").Split('\n'));
                var Values = IniReader.GetValues(Runtime);

                Version LastVersion = null;
                if (Values.ContainsKey("lastversion"))
                    LastVersion = new Version(Values["lastversion"]);

                if (Values.ContainsKey("brokenupdater"))
                {
                    var BrokenVer = new Version(Values["brokenupdater"]);
                    if (CurrentVersion <= BrokenVer)
                    {
                        Console.WriteLine("Auto Updater Broken - Please, Download the last update manually");
                        Environment.Exit(134);
                    }
                }

                return LastVersion > CurrentVersion;
            }
            catch
            {
                return false;
            }
        }


        public void DownloadUpdate()
        {
            if (!HasUpdates())
                return;

            MemoryStream Update = new MemoryStream(Download(Repo + Runtime + ".zip"));
            var Zip = ZipFile.Read(Update);
            
            try
            {
                if (Directory.Exists(TempUpdateDir))
                    Directory.Delete(TempUpdateDir, true);
            }
            catch { }

            Directory.CreateDirectory(TempUpdateDir);
            Zip.ExtractAll(TempUpdateDir, ExtractExistingFileAction.OverwriteSilently);
            
            if (File.Exists(TempUpdateDir + Path.GetFileNameWithoutExtension(MainExecutable) + ".Desktop" + Path.GetExtension(MainExecutable)))
                Process.Start(TempUpdateDir + Path.GetFileNameWithoutExtension(MainExecutable) + ".Desktop" + Path.GetExtension(MainExecutable));
            else
                Process.Start(TempUpdateDir + Path.GetFileName(MainExecutable));

            Environment.Exit(0);
        }

        public bool FinishUpdatePending()
        {
            if (MainExecutable == null)
                return false;

            if (MainExecutable.Contains("\\LastVersion\\"))
                return true;

            if (Directory.Exists(TempUpdateDir))
                Directory.Delete(TempUpdateDir, true);

            return false;
        }

        public string FinishUpdate()
        {
            if (FinishUpdatePending())
            {
                int Len = MainExecutable.IndexOf("\\LastVersion\\");

                string OriginalPath = MainExecutable.Substring(0, Len);
                string RunningDir = Path.GetDirectoryName(MainExecutable);

                if (!RunningDir.EndsWith("\\"))
                    RunningDir += '\\';
                if (!OriginalPath.EndsWith("\\"))
                    OriginalPath += '\\';

                foreach (string File in Directory.GetFiles(RunningDir, "*.*", SearchOption.AllDirectories))
                {
                    string Base = File.Substring(RunningDir.Length).TrimStart('\\');
                    string UpPath = RunningDir + Base;
                    string OlPath = OriginalPath + Base;


                    Delete(OlPath);
                    System.IO.File.Copy(UpPath, OlPath, true);
                }

                var PossiblePath = OriginalPath + Path.GetFileName(MainExecutable);

                if (File.Exists(OriginalPath + Path.GetFileNameWithoutExtension(MainExecutable) + ".Desktop" + Path.GetExtension(MainExecutable)))
                    PossiblePath = OriginalPath + Path.GetFileNameWithoutExtension(MainExecutable) + ".Desktop" + Path.GetExtension(MainExecutable);

                return PossiblePath;
            }
            else
            {
                new Thread(() => {
                    int i = 0;
                    while (Directory.Exists(TempUpdateDir) && i < 5)
                    {
                        try
                        {
                            Directory.Delete(TempUpdateDir, true);
                        }
                        catch { Thread.Sleep(1000); i++; }
                    }
                }).Start();
                return null;
            }
        }

        private void Delete(string File)
        {
            for (int Tries = 0; Tries < 10; Tries++)
            {
                try
                {
                    string ProcName = Path.GetFileNameWithoutExtension(MainExecutable);
                    Process[] Procs = Process.GetProcessesByName(ProcName);
                    int ID = Process.GetCurrentProcess().Id;
                    foreach (var Proc in Procs)
                    {
                        if (Proc.Id == ID)
                            continue;

                        try
                        {
                            Proc.Kill();
                            Thread.Sleep(100);
                        }
                        catch { }
                    }

                    if (System.IO.File.Exists(File))
                        System.IO.File.Delete(File);
                }
                catch
                {
                    Thread.Sleep(100);
                    continue;
                }

                break;
            }
        }

        public string DownloadString(string URL) {
            try
            {
                return Encoding.UTF8.GetString(Download(URL));
            }
            catch
            {
                return null;
            }
        }

        private byte[] Download(string URL)
        {
            MemoryStream MEM = new MemoryStream();
            Download(URL, MEM);
            byte[] DATA = MEM.ToArray();
            MEM.Close();
            return DATA;
        }

        private void Download(string URL, Stream Output, int tries = 4)
        {
            try
            {
                HttpWebRequest Request = (HttpWebRequest)WebRequest.Create(URL);

                Request.UseDefaultCredentials = true;
                Request.Method = "GET";
                WebResponse Response = Request.GetResponse();
                byte[] FC = new byte[0];
                using (Stream Reader = Response.GetResponseStream())
                {
                    byte[] Buffer = new byte[1024];
                    int bytesRead;
                    do
                    {
                        bytesRead = Reader.Read(Buffer, 0, Buffer.Length);
                        Output.Write(Buffer, 0, bytesRead);
                    } while (bytesRead > 0);
                }

                Response.Dispose();
            }
            catch (Exception ex)
            {
                if (tries < 0)
                    throw new Exception(string.Format("Connection Error: {0}", ex.Message));

                Thread.Sleep(1000);
                Download(URL, Output, tries - 1);
            }
        }


    }
}
