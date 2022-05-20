namespace DirectPackageInstaller.Proxy
{
    class ProxyScan : IProxy
    {
        public string[] GetProxies()
        {
            lock (App.HttpClient)
            {
                App.HttpClient.Proxy = null;
                App.HttpClient.Headers.Clear();
                return App.HttpClient.DownloadString("https://www.proxyscan.io/api/proxy?type=http&format=txt&limit=10").Replace("\r\n", "\n").Split('\n');
            }
        }
    }
}
