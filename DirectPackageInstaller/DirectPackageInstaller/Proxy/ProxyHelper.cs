using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DirectPackageInstaller.Proxy
{
    static class ProxyHelper 
    {
        static string[] Proxies;

        static int Index;

        public static string Proxy 
        {
            get {
                if (Proxies == null || Index >= Proxies.Length)
                    RefreshProxy();

                return Proxies[Index++];
            }
        }

        public static WebProxy WebProxy => new WebProxy(Proxy, true);

        static IProxy[] APIs = new IProxy[] { 
            new ProxyScan(), new ProxyScrape()
        };

        private static void RefreshProxy()
        {
            Index = 0;

            List<string> List = new List<string>();

            foreach (var API in APIs)
            {
                try
                {
                    List.AddRange(API.GetProxies());
                }
                catch { }
            }

            Proxies = List.ToArray();
        }
    }
}
