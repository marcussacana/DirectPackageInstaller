namespace DirectPackageInstaller.Proxy
{
    class ProxyScrape : IProxy
    {
        public string[] GetProxies()
        {
            lock (App.HttpClient)
            {
                App.HttpClient.Proxy = null;
                App.HttpClient.Headers.Clear();
                return App.HttpClient.DownloadString("https://api.proxyscrape.com/v2/?request=displayproxies&protocol=http&timeout=20000&country=all&ssl=all&anonymity=all&simplified=true").Replace("\r\n", "\n").Split('\n');
            }
        }
    }
}
