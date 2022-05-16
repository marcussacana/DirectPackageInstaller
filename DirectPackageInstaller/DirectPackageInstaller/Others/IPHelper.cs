using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using DirectPackageInstaller.Views;

namespace DirectPackageInstaller
{
    static class IPHelper
    {
        public static string FindLocalIP(string RemoteIP)
        {
            try
            {
                var IPs = SearchInterfaces();
                string RemotePartial = RemoteIP.Substring(0, RemoteIP.LastIndexOf('.'));
                return IPs.Where(x => x.StartsWith(RemotePartial)).Single();
            }
            catch
            {
                return null;
            }
        }

        static string[] SearchInterfaces()
        {
            List<string> IPs = new List<string>();
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

                            if (!IPs.Contains(IpInfo.Address.ToString()))
                                IPs.Add(IpInfo.Address.ToString());
                        }
                        catch { }
                    }
                }
                catch { }
            }

            return IPs.ToArray();
        }

        static HttpClient Client = new HttpClient()
        {
            Timeout = TimeSpan.FromMilliseconds(1000)
        };

        public static bool IsRPIOnline(string IP)
        {
            if (!IPAddress.TryParse(IP, out _))
                return false;
#if DEBUG
            //if (System.Diagnostics.Debugger.IsAttached)
            //    return true;
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
