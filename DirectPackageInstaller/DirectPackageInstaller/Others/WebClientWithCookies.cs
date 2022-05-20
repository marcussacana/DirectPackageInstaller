using System;
using System.Net;

namespace DirectPackageInstaller
{
    class WebClientWithCookies : WebClient
    {
        public CookieContainer Container = new CookieContainer();

        protected override WebRequest GetWebRequest(Uri address)
        {
            HttpWebRequest request = base.GetWebRequest(address) as HttpWebRequest;

            if (request != null)
                request.CookieContainer = Container;

            return request;
        }
    }
}
