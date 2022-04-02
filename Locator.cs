using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DirectPackageInstaller
{
    static class Locator
    {
        static bool Searching = true;
        public static Action<string> OnPS4DeviceFound = null;

        internal static List<string> LocalIPs = new List<string>();
        internal static List<string> Devices = new List<string>();
        internal static void Locate(bool Persist)
        {
            do
            {
                SearchInterfaces();

                foreach (NetworkInterface Interface in NetworkInterface.GetAllNetworkInterfaces().Where(x => x.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || x.NetworkInterfaceType == NetworkInterfaceType.Ethernet))
                {

                    try
                    {
                        foreach (UnicastIPAddressInformation IpInfo in Interface.GetIPProperties().UnicastAddresses.Where(x => x.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork))
                        {
                            try
                            {
                                if (Interface.OperationalStatus != OperationalStatus.Up)
                                    continue;

                                if (Devices.Count > 0)
                                    return;

                                var Address = IpInfo.Address.GetAddressBytes();

                                if (!FindPS4(Address, 100, 256))
                                    FindPS4(Address, 0, 100);
                            }
                            catch { }
                        }
                    }
                    catch { }
                }

                Thread.Sleep(3000);
            } while (Devices.Count == 0 && Persist);

        }

        public static bool FindPS4(byte[] Address, int Begin, int End)
        {
            var CancelToken = new CancellationTokenSource();
            var ParallelOpt = new ParallelOptions() { MaxDegreeOfParallelism = 10, CancellationToken = CancelToken.Token };
            bool DeviceFound = false;

            ParallelLoopResult Result = default;
            try
            {
                Result = Parallel.For(100, 256, ParallelOpt, (i) =>
                {
                    if (Devices.Count > 0)
                        return;

                    var IP = $"{Address[0]}.{Address[1]}.{Address[2]}.{i}";
                    if (IsValidPS4IP(IP))
                    {
                        DeviceFound = true;
                        Devices.Add(IP);
                        OnPS4DeviceFound?.Invoke(IP);
                        CancelToken.Cancel();
                    }
                });
            }
            catch { }

            while (!Result.IsCompleted)
                Thread.Sleep(100);

            return DeviceFound;
        }

        public static string FindLocalIP(string RemoteIP)
        {
            try
            {
                if (Searching)
                    SearchInterfaces();
                string RemotePartial = RemoteIP.Substring(0, RemoteIP.LastIndexOf('.'));
                return LocalIPs.Where(x => x.StartsWith(RemotePartial)).Single();
            }
            catch (Exception ex)
            {
#if DEBUG
                MessageBox.Show(ex.ToString(), "");
#else
                System.IO.File.WriteAllText("DPI.log", "Error: " + ex.ToString());
#endif
                return null;
            }
        }

        static void SearchInterfaces()
        {
            foreach (NetworkInterface Interface in NetworkInterface.GetAllNetworkInterfaces().Where(x => x.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || x.NetworkInterfaceType == NetworkInterfaceType.Ethernet))
            {
                try
                {
                    foreach (UnicastIPAddressInformation IpInfo in Interface.GetIPProperties().UnicastAddresses.Where(x => x.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork))
                    {
                        try
                        {
                            if (Interface.OperationalStatus != OperationalStatus.Up)
                                continue;

                            if (!LocalIPs.Contains(IpInfo.Address.ToString()))
                                LocalIPs.Add(IpInfo.Address.ToString());
                        }
                        catch { }
                    }
                }
                catch { }
            }

            Searching = false;
        }

        static HttpClient Client = new HttpClient()
        {
            Timeout = TimeSpan.FromMilliseconds(1000)
        };

        public static bool IsValidPS4IP(string IP)
        {
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
                return true;
#endif
            var APIUrl = $"http://{IP}:12800/api";
            try
            {
                using var Response = Client.GetAsync(APIUrl).GetAwaiter().GetResult();
                var Resp = Response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (Resp.Contains("Unsupported method") && Resp.Contains("fail"))
                    return true;
            }
            catch { }
            return false;
        }
    }
}
