using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DirectPackageInstaller
{
    static class Locator
    {
        internal static List<string> Devices = new List<string>();
        internal static void Locate()
        {
            while (Devices.Count == 0)
            {
                foreach (NetworkInterface Interface in NetworkInterface.GetAllNetworkInterfaces())
                    if (Interface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || Interface.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                        foreach (UnicastIPAddressInformation IpInfo in Interface.GetIPProperties().UnicastAddresses)
                            if (IpInfo.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            {

                                var Address = IpInfo.Address.GetAddressBytes();
                                for (int i = 100; i < 255; i++)
                                {
                                    var IP = $"{Address[0]}.{Address[1]}.{Address[2]}.{i}";
                                    if (IsValidPS4IP(IP))
                                        Devices.Add(IP);
                                }
                                for (int i = 1; i < 99; i++)
                                {
                                    var IP = $"{Address[0]}.{Address[1]}.{Address[2]}.{i}";
                                    if (IsValidPS4IP(IP))
                                        Devices.Add(IP);
                                }
                            }

                Thread.Sleep(3000);
            }

        }

        public static bool IsValidPS4IP(string IP)
        {
            var Client = new HttpClient();
            Client.Timeout = TimeSpan.FromMilliseconds(500);
            var APIUrl = $"http://{IP}:12800/api";

            try
            {
                var Response = Client.GetAsync(APIUrl).GetAwaiter().GetResult();
                var Resp = Response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (Resp.Contains("Unsupported method") && Resp.Contains("fail"))
                    return true;

            }
            catch { }
            return false;
        }
    }
}
