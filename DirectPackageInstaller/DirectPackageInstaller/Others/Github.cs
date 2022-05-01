using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading;
using DirectPackageInstaller;

class GitHub {

    string API = "https://api.github.com/repos/{0}/{1}/releases";

    string cache = null;
    string Name = null;
    public static string MainExecutable = App.IsUnix ? Directory.GetFiles(App.WorkingDirectory, "DirectPackageInstaller*").FirstOrDefault() : new Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBase).LocalPath;
    public static string TempUpdateDir = Path.GetDirectoryName(MainExecutable) + "\\GitHubRelease\\";
    public static string CurrentVersion {
        get {
            var Version = FileVersionInfo.GetVersionInfo(MainExecutable);
            return Version.FileMajorPart + "." + Version.FileMinorPart + "." + Version.FileBuildPart;
        }
    }
    public GitHub(string Username, string Project) {
        API = string.Format(API, Username, Project);

        if (!File.Exists(MainExecutable))
            throw new Exception("Failed to Catch the Executable Path");
    }
    public GitHub(string Username, string Project, string Name) {
        API = string.Format(API, Username, Project);
        this.Name = Name;
        
        if (System.Diagnostics.Debugger.IsAttached)
            return;
        
        if (!File.Exists(MainExecutable))
            throw new Exception("Failed to Catch the Executable Path");
    }

    public string FinishUpdate() {
        if (FinishUpdatePending()) {
            int Len = MainExecutable.IndexOf("\\GitHubRelease\\");

            string OriginalPath = MainExecutable.Substring(0, Len);
            string RunningDir = Path.GetDirectoryName(MainExecutable);

            if (!RunningDir.EndsWith("\\"))
                RunningDir += '\\';
            if (!OriginalPath.EndsWith("\\"))
                OriginalPath += '\\';

            foreach (string File in Directory.GetFiles(RunningDir, "*.*", SearchOption.AllDirectories)) {
                string Base = File.Substring(RunningDir.Length).TrimStart('\\');
                string UpPath = RunningDir + Base;
                string OlPath = OriginalPath + Base;


                Delete(OlPath);
                System.IO.File.Copy(UpPath, OlPath, true);
            }

            return OriginalPath + Path.GetFileName(MainExecutable);
        } else {
            new Thread(() => {
                int i = 0;
                while (Directory.Exists(TempUpdateDir) && i < 5) {
                    try {
                        Directory.Delete(TempUpdateDir, true);
                    } catch { Thread.Sleep(1000); i++; }
                }
            }).Start();
            return null;
        }
    }

    private void Delete(string File) {
        for (int Tries = 0; Tries < 10; Tries++) {
            try {
                string ProcName = Path.GetFileNameWithoutExtension(MainExecutable);
                Process[] Procs = Process.GetProcessesByName(ProcName);
                int ID = Process.GetCurrentProcess().Id;
                foreach (var Proc in Procs) {
                    if (Proc.Id == ID)
                        continue;

                    try {
                        Proc.Kill();
                        Thread.Sleep(100);
                    } catch { }
                }

                if (System.IO.File.Exists(File))
                    System.IO.File.Delete(File);
            } catch {
                Thread.Sleep(100);
                continue;
            }

            break;
        }
    }
    public bool HaveUpdate() {
        try {
            if (Debugger.IsAttached)
                return false;

            string CurrentVersion = FileVersionInfo.GetVersionInfo(MainExecutable).FileVersion.Trim();
            string LastestVersion = GetLastestVersion().Trim();
            int[] CurrArr = CurrentVersion.Split('.').Select(x => int.Parse(x)).ToArray();
            int[] LastArr = LastestVersion.Split('.').Select(x => int.Parse(x)).ToArray();
            int Max = CurrArr.Length < LastArr.Length ? CurrArr.Length : LastArr.Length;
            for (int i = 0; i < Max; i++) {
                if (LastArr[i] > CurrArr[i])
                    return true;
                if (LastArr[i] == CurrArr[i])
                    continue;
                return false;//Lst<Curr
            }
            return false;
        } catch (Exception ex){ return false; }
    }

    public bool FinishUpdatePending()
    {
        if (MainExecutable == null)
            return false;
        
        if (MainExecutable.Contains("\\GitHubRelease\\"))
            return true;

        if (Directory.Exists(TempUpdateDir))
            Directory.Delete(TempUpdateDir, true);    
        
        return false;
    }
    public void Update() {
        if (!HaveUpdate())
            return;

        string Result = FinishUpdate();
        if (Result != null) {
            Process.Start(Result);
            Environment.Exit(0);
        }

        MemoryStream Update = new MemoryStream(Download(GetDownloadUrl()));
        var Zip = new ZipArchive(Update);
        try {
            if (Directory.Exists(TempUpdateDir))
                Directory.Delete(TempUpdateDir, true);
        } catch { }

        Directory.CreateDirectory(TempUpdateDir);
        Zip.ExtractToDirectory(TempUpdateDir);
        Process.Start(TempUpdateDir + Path.GetFileName(MainExecutable));
        Environment.Exit(0);
    }

    private void Backup(string Path) {
        if (File.Exists(Path + ".bak"))
            File.Delete(Path + ".bak");
        while (File.Exists(Path)) {
            try {
                File.Move(Path, Path + ".bak");
            } catch { Thread.Sleep(100); }
        }
    }

    private byte[] Download(string URL) {
        MemoryStream MEM = new MemoryStream();
        Download(URL, MEM);
        byte[] DATA = MEM.ToArray();
        MEM.Close();
        return DATA;
    }

    private void Download(string URL, Stream Output, int tries = 4) {
        try {
            HttpWebRequest Request = (HttpWebRequest)WebRequest.Create(URL);
            //Bypass a fucking bug in the fucking .net framework
            if (Request.Address.AbsoluteUri != URL && tries <= 2) {
                /*
                WebClient WC = new WebClient();
                WC.QueryString.Add("action", "shorturl");
                WC.QueryString.Add("format", "simple");
                WC.QueryString.Add("url", URL);
                URL = WC.DownloadString("https://u.nu/api.php");*/

                Request = (HttpWebRequest)WebRequest.Create("http://proxy-it.nordvpn.com/browse.php?u=" + URL);
                Request.Referer = "http://proxy-it.nordvpn.com";
            }

            Request.UseDefaultCredentials = true;
            Request.Method = "GET";
            WebResponse Response = Request.GetResponse();
            byte[] FC = new byte[0];
            using (Stream Reader = Response.GetResponseStream()) {
                byte[] Buffer = new byte[1024];
                int bytesRead;
                do {
                    bytesRead = Reader.Read(Buffer, 0, Buffer.Length);
                    Output.Write(Buffer, 0, bytesRead);
                } while (bytesRead > 0);
            }
        } catch (Exception ex) {
            if (tries < 0)
                throw new Exception(string.Format("Connection Error: {0}", ex.Message));

            Thread.Sleep(1000);
            Download(URL, Output, tries - 1);
        }
    }

   
    
    private string GetLastestVersion() {
        string Reg = @"\""tag_name\"":[\s]*\""[A-z]*([0-9.]*)[A-z]*\""";
        var a = System.Text.RegularExpressions.Regex.Match(GetApiResult(), Reg);
        return a.Groups[1].Value;
    }
    private string GetDownloadUrl() {
        string Reg = @"\""browser_download_url\"":[\s]*\""(.*)\""";
        var Matches = System.Text.RegularExpressions.Regex.Matches(GetApiResult(), Reg);
        foreach (var Match in Matches.Cast<System.Text.RegularExpressions.Match>())
        {
            if (Name == null)
                return Match.Groups[1].Value;
            for (int i = 0; i < Match.Groups.Count; i++)
            {
                string URL = Match.Groups[i].Value;
                URL = URL.Split('?')[0].ToLower();
                if (URL.Replace(".zip", "").EndsWith(Name.ToLower().Replace(".zip", "")))
                    return Match.Groups[i].Value;
            }
        }
        throw new FileNotFoundException("Github Release File Not Found.");
    }

    private string GetApiResult() {
        if (cache != null)
            return cache;

        BypassSLL();

        WebClient Client = new WebClient();
        Client.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/84.0.4147.125 Safari/537.36 Edg/84.0.522.59");
        cache = Client.DownloadString(API);
        return cache;
    }

    public void BypassSLL() {
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)0x00000FF0;
        ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
    }
}