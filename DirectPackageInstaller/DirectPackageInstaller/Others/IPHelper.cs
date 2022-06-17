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
    public static class IPHelper
    {
        public static string[] EnumLocalIPs()
        {
            return SearchInterfaces();
        }
        public static string? FindLocalIP(string RemoteIP)
        {
            try
            {
                var IPs = SearchInterfaces();
                string RemotePartial = RemoteIP.Substring(0, RemoteIP.LastIndexOf('.'));
                return IPs.Single(x => x.StartsWith(RemotePartial));
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

        public static async Task<bool> IsRPIOnline(string IP)
        {
            if (!IPAddress.TryParse(IP, out _))
                return false;
            
            var APIUrl = $"http://{IP}:12800/api";
            try
            {
                using var Response = await Client.GetAsync(APIUrl);
                var Resp = await Response.Content.ReadAsStringAsync();
                if (Resp.Contains("Unsupported method") && Resp.Contains("fail"))
                    return true;
#if DEBUG
                await MessageBox.ShowAsync("RPI RESP: " + Resp, "DPI", MessageBoxButtons.OK, MessageBoxIcon.Error);
#endif
            }
            catch (Exception ex)
            {
#if DEBUG
                await MessageBox.ShowAsync("RPI ERROR: " + ex.ToString(), "DPI", MessageBoxButtons.OK, MessageBoxIcon.Error);
#endif
            }
            return false;
        }
    }
}
