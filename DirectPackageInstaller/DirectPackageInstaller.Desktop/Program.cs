using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using DirectPackageInstaller.Host;
using DirectPackageInstaller.IO;
using DirectPackageInstaller.Tasks;

namespace DirectPackageInstaller.Desktop
{
    class Program
    {
        const uint SW_SHOW = 5;

        static IntPtr hConsole = IntPtr.Zero;

        [DllImport("kernel32.dll")]
        static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, uint nCmdShow);


        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            if (args == null || args.Length == 0){
                TempHelper.Clear();
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args); 
                TempHelper.Clear();

                Environment.Exit(0);
                return;
            }

            if (OperatingSystem.IsWindows())
            {
                AllocConsole();
                Console.OutputEncoding = Encoding.Unicode;
                hConsole = GetConsoleWindow();
                ShowWindow(hConsole, SW_SHOW);
            }

            Console.Title = "DirectPacakgeInstaller";
            Console.WriteLine($"DirectPacakgeInstaller v{SelfUpdate.CurrentVersion} - CLI");

            bool Proxy = false;
            string Server = null;
            string PS4 = null;
            string URL = null;
            int Port = 0;

            for (int i = 0; i < args.Length; i++)
            {
                string Arg = args[i].Trim(' ', '-', '/', '\\').ToLowerInvariant();
                switch (Arg)
                {
                    case "lan":
                    case "server":
                    case "serverip":
                    case "lanip":
                        if (i + 1 >= args.Length)
                            goto case "help";
                        
                        Server = args[++i];
                        break;
                    case "ps4":
                    case "ps4ip":
                        if (i + 1 >= args.Length)
                            goto case "help";
                        PS4 = args[++i];
                        break;
                    case "port":
                    case "binloader":
                        if (i + 1 >= args.Length)
                            goto case "help";
                        int.TryParse(args[++i], out Port);
                        break;
                    case "proxy":
                        Proxy = true;
                        break;
                    case "help":
                    case "h":
                    case "?":
                        Console.WriteLine("The DirectPackageInstaller can do only basic things currently.");
                        Console.WriteLine("DirectPackageInstaller.Desktop -Server PKG_SENDER_PC_IP -PS4 PS4_IP -Port BIN_LOADER_PORT [-Proxy] http://eaxample.com/game.pkg");
                        Console.WriteLine();
                        Console.WriteLine("Where:");
                        Console.WriteLine();
                        Console.WriteLine("PKG_SENDER_PC_IP is the IP of the machine that are running the DirectPackageInstaller");
                        Console.WriteLine("The IP is automatically set then is optional, but isn't safe if you have multiple network connections");
                        Console.WriteLine();
                        Console.WriteLine("PS4 is the IP of your PS4 IP Address");
                        Console.WriteLine("The IP will make the process faster, but still optional, but the DPI will take a time to find your PS4 IP");
                        Console.WriteLine();
                        Console.WriteLine("BIN_LOADER_PORT is the port of the running bin loader in the PS4");
                        Console.WriteLine("By default the ports 9090, 9021 and 9020 are tried, then you usually don't need specify the port");
                        Console.WriteLine();
                        Console.WriteLine("The -Proxy parameter is optional, this will make the PS4 download url be proxied by the DirectPacakgeInstaller");
                        Console.WriteLine();
                        Console.WriteLine("The URL is the PKG URL, currently, must be a PKG file, RAR or 7Z files aren't accepted yet.");
                        Console.WriteLine("Instead URL you can put a absolute file path as well.");
                        Console.WriteLine();
                        Console.WriteLine("DirectPackageInstaller - By Marcussacana");
                        Console.ReadKey();
                        return;
                    default:
                        if (!Arg.StartsWith("http") && !File.Exists(args[i]))
                            goto case "help";
                        URL = args[i];
                        break;
                }
            }

            if (Server == null)
            {
                if (PS4 != null)
                {
                    Server = IPHelper.FindLocalIP(PS4);
                }
            }

            if (PS4 == null)
            {
                _ = PS4Finder.StartFinder((PSIP, PCIP) =>
                {
                    PS4 = PSIP.ToString();
                    
                    if (Server != null || PCIP == null)
                        return;
                    
                    Server = PCIP.ToString();
                });
                
                Console.WriteLine("Searching the PS4...");
                
                while (PS4 == null)
                {
                    Task.Delay(1000).ConfigureAwait(false).GetAwaiter().GetResult();
                }
            }


            if (Server == null)
            {
                Console.WriteLine("Failed to Detect your PC IP.");
                return;
            }
            
            Console.WriteLine($"LAN: {Server}");

            if (PS4 == null)
            {
                Console.WriteLine("Failed to Detect your PS4 IP");
                return;
            }
            
            Console.WriteLine($"PS4: {PS4}");
            
            if (URL == null)
            {
                Console.WriteLine("Missing PKG URL");
                return;
            }
            
            if (Port != 0)
                Console.WriteLine($"Port: {Port}");

            PS4Server PSServer = new PS4Server(Server);
            
            try
            {
                PSServer.Start();
            }
            catch
            {
                Console.WriteLine("ERROR: Another Instance of the DPI is Running?");
                throw;
            }

            bool FileInput = !URL.StartsWith("http") && File.Exists(URL);

            var DirectURL = URL;


            Console.WriteLine($"Source: {DirectURL}");

            Stream PKG = null;

            DownloaderTask? DownTask = null;

            bool LimitedFHost = false;

            if (FileInput)
            {
                Proxy = true;
                PKG = new FileStream(DirectURL, FileMode.Open);
                URL = $"http://{Server}:{PSServer.Server.Settings.Port}/file/?b64={Convert.ToBase64String(Encoding.UTF8.GetBytes(URL))}";
            }
            else
            {
                var HostStream = new FileHostStream(DirectURL);
                
                if (HostStream.DirectLink && !HostStream.SingleConnection && !Proxy)
                    URL = HostStream.Url;
                
                if ((!HostStream.DirectLink || Proxy) && !HostStream.SingleConnection)
                {
                    Proxy = true;
                    URL = $"http://{Server}:{PSServer.Server.Settings.Port}/proxy/?b64={Convert.ToBase64String(Encoding.UTF8.GetBytes(URL))}";
                }

                if (HostStream.SingleConnection)
                {
                    HostStream.KeepAlive = true;
                    
                    Console.WriteLine("WARNING: Limited File Hosting - The given url is supported but not recommended.");
                    DownTask = Downloader.CreateTask(URL, HostStream);

                    PKG = new VirtualStream(DownTask?.OpenRead() ?? throw new Exception(), 0, DownTask?.SafeLength ?? 0) {
                        ForceAmount = true
                    };

                    LimitedFHost = true;
                }
                else
                    PKG = HostStream;
            }

            var Info = PKG.GetPKGInfo();

            if (Info == null)
            {
                Console.WriteLine("Failed to get the PKG Info");
                return;
            }
            
            if (LimitedFHost)
            {
                Proxy = true;
                
                Console.WriteLine("Preloading PKG...");
                
                while (DownTask?.SafeReadyLength < Info?.PreloadLength)
                    Task.Delay(1000).ConfigureAwait(false).GetAwaiter().GetResult();
                
                URL = $"http://{Server}:{PSServer.Server.Settings.Port}/cache/?b64={Convert.ToBase64String(Encoding.UTF8.GetBytes(URL))}";
            }
            else
            {
                PKG.Close();
            }
            
            Console.WriteLine($"Pusing: {Info?.FriendlyName}");
            Console.WriteLine($"Title ID: {Info?.TitleID}");
            Console.WriteLine($"Content ID: {Info?.ContentID}");
            Console.WriteLine($"Type: {Info?.FirendlyContentType}");

            Installer.Server = PSServer;
            Installer.CurrentPKG = Info!.Value;
            
            bool Status = Installer.Payload.SendPKGPayload(PS4, Server, URL, true, false).ConfigureAwait(false).GetAwaiter().GetResult();

            if (!Status)
            {
                Console.WriteLine("Failed to Send the PKG");
                return;
            }

            while (true)
            {
                Task.Delay(1000).ConfigureAwait(false).GetAwaiter().GetResult();
                
                if (PSServer.LastRequest == null)
                    continue;
                
                if (PSServer.Connections > 0)
                    continue;

                if (DownTask is {Running: true})
                    continue;

                var IDLESeconds = (DateTime.Now - PSServer.LastRequest!.Value).TotalSeconds;
                
                if (Proxy && PSServer.LastRequestMode is "json" or null && IDLESeconds < 60)
                    continue;

                if (IDLESeconds > 5)
                    break;
            }
            
            TempHelper.Clear();
            
            Console.WriteLine("Sent!");
            
            Environment.Exit(0);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();
    }
}