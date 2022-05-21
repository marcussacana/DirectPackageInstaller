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
        
        public static async Task StartFinder(Action<IPAddress, IPAddress?> OnFound)
        {
            if (OnFound == null)
                return;

            var IPs = IPHelper.EnumLocalIPs();

            var Found = OnFound;

            bool Searching = true;

            SocketAsyncEventArgs OnReceived = new SocketAsyncEventArgs();
            OnReceived.Completed += (sender, e) =>
            {
                if (e.RemoteEndPoint is IPEndPoint)
                {
                    var PS4IP = ((IPEndPoint) e.RemoteEndPoint).Address;

                    IPAddress? PCIP = ((IPEndPoint?) ((Socket) sender).LocalEndPoint)?.Address;

                    if (PCIP!= null && PCIP.Equals(IPAddress.Any))
                        PCIP = null;
                    
                    if (!PS4IP.Equals(IPAddress.Any))
                    {
                        Found(PS4IP, PCIP);
                        Searching = false;
                    }
                }
            };


            int IPIndex = 0;
            
            while (Searching)
            {
                var LocalIP = IPs.Length == 0 ? null : IPs[IPIndex++];

                if (IPIndex >= IPs.Length)
                    IPIndex = 0;
                
                Socket Discovery = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                Discovery.EnableBroadcast = true;
                Discovery.ExclusiveAddressUse = false;
                Discovery.ReceiveTimeout = 3000;

                if (LocalIP != null)
                {
                    Discovery.Bind(new IPEndPoint(IPAddress.Parse(LocalIP), 0));
                    Discovery.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
                    Discovery.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontRoute, 1);
                }

                byte[] Buffer = new byte[Discovery.ReceiveBufferSize];
                OnReceived.SetBuffer(Buffer);
                OnReceived.RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

                Discovery.ReceiveFromAsync(OnReceived);


                for (int i = 0; i < 5; i++)
                {
                    var SendTo = new SocketAsyncEventArgs()
                    {
                        RemoteEndPoint = new IPEndPoint(IPAddress.Broadcast, 987)
                    };

                    SendTo.SetBuffer(SearchData, 0, SearchData.Length);
                    Discovery.SendToAsync(SendTo);

                    await Task.Delay(500);
                }

                Discovery.Close();
            }
        }
    }
    
}