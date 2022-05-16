using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace DirectPackageInstaller
{
    public static class PS4Finder
    {
        public const string SearchMessage = "SRCH * HTTP/1.1\ndevice-discovery-protocol-version:00030010\n";
        public static byte[] SearchData => Encoding.UTF8.GetBytes(SearchMessage);
        
        public static async Task StartFinder(Action<IPAddress> OnFound)
        {
            if (OnFound == null)
                return;
        
            var Found = OnFound;

            bool Searching = true;

            Socket Discovery = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            Discovery.EnableBroadcast = true;
            Discovery.ExclusiveAddressUse = false;
            Discovery.ReceiveTimeout = 3000;

            SocketAsyncEventArgs OnReceived = new SocketAsyncEventArgs();
            OnReceived.Completed += (sender, e) =>
            {
                if (e.RemoteEndPoint is IPEndPoint)
                {
                    Found(((IPEndPoint)e.RemoteEndPoint).Address);
                    Searching = false;
                }
            };
            
            byte[] Buffer = new byte[Discovery.ReceiveBufferSize];
            OnReceived.SetBuffer(Buffer);
            OnReceived.RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            
            Discovery.ReceiveFromAsync(OnReceived);


            while (Searching)
            {
                var SendTo = new SocketAsyncEventArgs()  {
                    RemoteEndPoint = new IPEndPoint(IPAddress.Broadcast, 987)
                };
                
                SendTo.SetBuffer(SearchData, 0, SearchData.Length);
                Discovery.SendToAsync(SendTo);
                
                await Task.Delay(1000);
            }
            
            Discovery.Close();
        }
    }
    
}