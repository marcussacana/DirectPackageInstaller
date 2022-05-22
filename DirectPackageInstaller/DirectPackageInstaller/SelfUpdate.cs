using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DirectPackageInstaller.IO;

namespace DirectPackageInstaller
{
    public class SelfUpdate
    {
        public static string MainExecutable
        {
            get
            {
                var MainAssembly = System.Reflection.Assembly.GetEntryAssembly().Location;
                
                if (File.Exists(Path.ChangeExtension(MainAssembly, "exe")))
                    return Path.ChangeExtension(MainAssembly, "exe");
                
                if (File.Exists(Path.ChangeExtension(MainAssembly, "").TrimEnd('.')))
                    return Path.ChangeExtension(MainAssembly, "").TrimEnd('.');
                
                return MainAssembly;
            }
        }

        private static readonly string TempUpdateDir = Path.Combine(Path.GetDirectoryName(MainExecutable), "LastVersion");

        const string Repo = "https://raw.githubusercontent.com/marcussacana/DirectPackageInstaller/Updater/";
        //const string Repo = "http://localhost:8000/";

        const string UpdateList = "Update.ini";

        public static Version CurrentVersion = new Version("6.0.3");

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
        }


        public async Task<bool> HasUpdates()
        {
            try
            {
                string UpdateInfo = await DownloadStringAsync(Repo + UpdateList);
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


        public async Task DownloadUpdate()
        {
            if (!await HasUpdates())
                return;

            MemoryStream Update = new MemoryStream(await DownloadAsync(Repo + Runtime + ".zip"));
            var Zip = new ZipArchive(Update);
            
            try
            {
                if (Directory.Exists(TempUpdateDir))
                    Directory.Delete(TempUpdateDir, true);
            }
            catch { }

            Directory.CreateDirectory(TempUpdateDir);
            
            Zip.ExtractToDirectory(TempUpdateDir, true);
            
            Process.Start(Path.Combine(TempUpdateDir, Path.GetFileName(MainExecutable)));

            Environment.Exit(0);
        }

        public bool FinishUpdatePending()
        {
            if (MainExecutable == null)
                return false;

            if (MainExecutable.Replace("\\", "/").Contains("/LastVersion/"))
                return true;

            if (Directory.Exists(TempUpdateDir))
                Directory.Delete(TempUpdateDir, true);
                
            if (File.Exists(Path.Combine(Path.GetDirectoryName(MainExecutable), "DirectPackageInstaller.exe")))
                Delete(Path.Combine(Path.GetDirectoryName(MainExecutable), "DirectPackageInstaller.exe"));
            return false;
        }

        public string FinishUpdate()
        {
            if (FinishUpdatePending())
            {
                int Len = MainExecutable.IndexOf("LastVersion");

                string OriginalPath = MainExecutable.Substring(0, Len);
                string RunningDir = Path.GetDirectoryName(MainExecutable);

                foreach (string File in Directory.GetFiles(RunningDir, "*.*", SearchOption.AllDirectories))
                {
                    string Base = File.Substring(RunningDir.Length).TrimStart('\\', '/');
                    string UpPath = Path.Combine(RunningDir, Base);
                    string OlPath = Path.Combine(OriginalPath, Base);
                    

                    Delete(OlPath);
                    System.IO.File.Copy(UpPath, OlPath, true);
                }
                
                return Path.Combine(OriginalPath, Path.GetFileName(MainExecutable));
            }

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

        public async Task<string> DownloadStringAsync(string URL) {
            try
            {
                return Encoding.UTF8.GetString(await DownloadAsync(URL));
            }
            catch
            {
                return null;
            }
        }

        private byte[] Download(string URL)
        {
            try
            {
                MemoryStream MEM = new MemoryStream();

                PartialHttpStream File = new PartialHttpStream(URL);
                File.CopyTo(MEM);

                byte[] DATA = MEM.ToArray();
                MEM.Close();
                return DATA;
            }
            catch
            {
                return null;
            }
        }

        private async Task<byte[]> DownloadAsync(string URL)
        {
            try
            {
                MemoryStream MEM = new MemoryStream();

                PartialHttpStream File = new PartialHttpStream(URL);
                await File.CopyToAsync(MEM);

                byte[] DATA = MEM.ToArray();
                MEM.Close();
                return DATA;
            }
            catch
            {
                return null;
            }
        }

    }
}