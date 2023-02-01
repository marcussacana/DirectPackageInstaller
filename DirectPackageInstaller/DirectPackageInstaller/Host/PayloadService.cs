using DirectPackageInstaller.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DirectPackageInstaller.Tasks;

namespace DirectPackageInstaller.Host
{
    public class PayloadService
    {

        ~PayloadService()
        {
            StopServer().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private Socket? ServiceSocket = null;
        private Socket? PayloadSocket;

        public bool ClientRunning { get; private set; } = false;

        private bool ServerRunning = false;

        private Queue<Socket> Queue = new Queue<Socket>();

        public async Task<bool> SendPKGPayload(string PS4IP, string PCIP, string URL, bool Silent)
        {
            if (Installer.Server == null)
                return false;

            URL = Installer.Server.RegisterJSON(URL, PCIP, Installer.CurrentPKG);

            if (!await EnsureServer(PS4IP))
                return false;

            if (!EnsureClient(PCIP))
                return false;

            DateTime WaitBegin = DateTime.Now;
            while (Queue.Count == 0 && (DateTime.Now - WaitBegin).TotalSeconds < 10)
            {
                if (!ServerRunning)
                    await EnsureServer(PS4IP);

                await Task.Delay(100);
            }

            if (Queue.Count == 0)
                return false;


            var PKGInfoSocket = Queue.Dequeue();
            
            ClientRunning = Queue.Count > 0;

            var UrlData = Encoding.UTF8.GetBytes(URL);
            var NameData = Encoding.UTF8.GetBytes(Installer.CurrentPKG.FriendlyName);
            var IDData = Encoding.UTF8.GetBytes(Installer.CurrentPKG.ContentID);
            var PKGType = Encoding.UTF8.GetBytes(Installer.CurrentPKG.BGFTContentType);
            var PackageSize = BitConverter.GetBytes(Installer.CurrentPKG.PackageSize);
            var IconData = Installer.CurrentPKG.IconData;

            if (IconData == null)
                IconData = new byte[0];

            List<byte> PKGInfoBuffer = new List<byte>();

            //1 = New Package, 0 = Service Exit
            PKGInfoBuffer.AddRange(BitConverter.GetBytes(1u));
            
            PKGInfoBuffer.AddRange(BitConverter.GetBytes(UrlData.Length));
            PKGInfoBuffer.AddRange(UrlData);
            PKGInfoBuffer.AddRange(BitConverter.GetBytes(NameData.Length));
            PKGInfoBuffer.AddRange(NameData);
            PKGInfoBuffer.AddRange(BitConverter.GetBytes(IDData.Length));
            PKGInfoBuffer.AddRange(IDData);
            PKGInfoBuffer.AddRange(BitConverter.GetBytes(PKGType.Length));
            PKGInfoBuffer.AddRange(PKGType);

            PKGInfoBuffer.AddRange(PackageSize);

            if (IconData.Length == 0)
            {
                PKGInfoBuffer.AddRange(new byte[4]);
            }
            else
            {
                PKGInfoBuffer.AddRange(BitConverter.GetBytes(IconData.Length));
                PKGInfoBuffer.AddRange(IconData);
            }

            SocketAsyncEventArgs PkgInfoEvent = new SocketAsyncEventArgs();
            PkgInfoEvent.RemoteEndPoint = PKGInfoSocket.RemoteEndPoint;
            PkgInfoEvent.SetBuffer(PKGInfoBuffer.ToArray());
            PkgInfoEvent.Completed += (sender, e) =>
            {
                PKGInfoSocket.Close();
                PayloadSocket?.Close();
            };

            PKGInfoSocket.SendAsync(PkgInfoEvent);

            if (!Silent)
                await MessageBox.ShowAsync("Package Sent!", "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Information);

            return true;
        }

        public async Task StopServer()
        {
            if (!ServerRunning)
                return;

            ServerRunning = false;

            while (ServiceSocket != null)
                await Task.Delay(100);

            while (Queue.Count != 0)
            {
                var Connection = Queue.Dequeue();

                TaskCompletionSource Source = new TaskCompletionSource();

                try
                {
                    SocketAsyncEventArgs ConnectionEvent = new SocketAsyncEventArgs();
                    ConnectionEvent.RemoteEndPoint = Connection.RemoteEndPoint;
                    ConnectionEvent.SetBuffer(new byte[4]);
                    ConnectionEvent.Completed += (sender, e) =>
                    {
                        Connection.Close();
                        PayloadSocket?.Close();
                        Source.SetResult();
                    };

                    Connection.SendAsync(ConnectionEvent);
                }
                catch
                {
                    Source.SetResult();
                }

                await Source.Task;
            }

            ClientRunning = false;
        }
        
        /// <summary>
        /// Tries to connect the PayloadSocket at the GoldHEN/MiraLoader payload port
        /// </summary>
        /// <param name="IP">The PS4 IP</param>
        /// <param name="Retry">For internal usage, don't set this parameter</param>
        /// <returns>When true the PayloadSocket holds a valid connection</returns>
        public async Task<bool> TryConnectSocket(string IP, bool Retry = true)
        {
            int[] Ports = new int[] { 9090, 9021, 9020 };
            foreach (var Port in Ports)
            {
                PayloadSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                PayloadSocket.ReceiveTimeout = 3000;
                PayloadSocket.SendTimeout = 3000;

                try
                {
                    await PayloadSocket.ConnectAsync(new IPEndPoint(IPAddress.Parse(IP), Port));
                    break;
                }
                catch { }
            }

            if (!PayloadSocket!.Connected && Retry)
            {
                await Task.Delay(3000);
                return await TryConnectSocket(IP, false);
            }

            return PayloadSocket.Connected;
        }

        /// <summary>
        /// Ensure the PKG Info Server is Listening
        /// </summary>
        /// <param name="PS4IP"></param>
        /// <returns></returns>
        private async Task<bool> EnsureServer(string PS4IP)
        {
            if (ServerRunning)
                return true;

            if (ServiceSocket == null)
            {
                ServiceSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                ServiceSocket.Bind(new IPEndPoint(IPAddress.Any, 0));
                ServiceSocket.Listen();
            }

            if (PayloadSocket == null || !PayloadSocket.Connected)
            {
                if (!await TryConnectSocket(PS4IP))
                    return false;
            }

            ServerRunning = true;

            _ = ServerLoop();

            return true;
        }

        /// <summary>
        /// Runs the PKG Info Socket connection accept loop.
        /// </summary>
        /// <exception cref="NullReferenceException"></exception>
        private async Task ServerLoop()
        {
            if (ServiceSocket == null)
                throw new NullReferenceException(nameof(ServiceSocket));

            do
            {
                CancellationTokenSource CToken = new CancellationTokenSource();
                CToken.CancelAfter(10000);

                try
                {
                    var ClientSocket = await ServiceSocket.AcceptAsync(CToken.Token);
                    Queue.Enqueue(ClientSocket);
                }
                catch
                {
                    continue;
                }
                finally
                {
                    ClientRunning = Queue.Count > 0;
                    CToken.Dispose();
                }
                
            } while (ServerRunning);

            ServiceSocket?.Close();
            ServiceSocket = null;
        }

        /// <summary>
        /// Ensure the Client Installer Payload is running in the PS4 System
        /// </summary>
        /// <param name="PCIP">The PC IP</param>
        /// <returns>If is running/started returns true, otherwise false</returns>
        private bool EnsureClient(string PCIP)
        {
            if (ClientRunning)
                return true;

            if (ServiceSocket == null)
                return false;

            if (PayloadSocket == null)
                return false;

            var Payload = Resources.Payload;

            var Offset = Payload.IndexOf(new byte[] { 0xB4, 0xB4, 0xB4, 0xB4, 0xB4, 0xB4 });
            if (Offset == -1)
                return false;

            ushort LocalPort = (ushort)((IPEndPoint)ServiceSocket.LocalEndPoint).Port;

            var IP = IPAddress.Parse(PCIP).GetAddressBytes();
            var Port = BitConverter.GetBytes(LocalPort).Reverse().ToArray();

            IP.CopyTo(Payload, Offset);
            Port.CopyTo(Payload, Offset + 4);

            PayloadSocket.SendBufferSize = Payload.Length;

            if (PayloadSocket.Send(Payload) != Payload.Length)
                return false;

            SocketAsyncEventArgs DisconnectEvent = new SocketAsyncEventArgs();
            DisconnectEvent.RemoteEndPoint = PayloadSocket.RemoteEndPoint;

            PayloadSocket.Disconnect(false);
            PayloadSocket.Close();

            return true;
        }
    }
}
