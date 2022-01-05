using static DirectPackageInstaller.Program;

namespace DirectPackageInstaller.Proxy
{
    class ProxyScrape : IProxy
    {
        public string[] GetProxies()
        {
            lock (HttpClient)
            {
                HttpClient.Proxy = null;
                HttpClient.Headers.Clear();
                return HttpClient.DownloadString("https://api.proxyscrape.com/v2/?request=displayproxies&protocol=http&timeout=20000&country=all&ssl=all&anonymity=all&simplified=true").Replace("\r\n", "\n").Split('\n');
            }
        }
    }
}
