using System;
using static DirectPackageInstaller.Program;

namespace DirectPackageInstaller.Proxy
{
    class ProxyScan : IProxy
    {
        public string[] GetProxies()
        {
            lock (HttpClient)
            {
                HttpClient.Proxy = null;
                HttpClient.Headers.Clear();
                return HttpClient.DownloadString("https://www.proxyscan.io/api/proxy?type=http&format=txt&limit=10").Replace("\r\n", "\n").Split('\n');
            }
        }
    }
}
