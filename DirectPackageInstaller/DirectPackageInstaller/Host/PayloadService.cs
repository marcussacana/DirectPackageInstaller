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

        /// <summary>
        /// A socket the console opened earlier looks alive until the moment we write to
        /// it, and by then the write goes nowhere while still reporting success. That
        /// is how a package used to be handed to a connection left over from the
        /// previous install, leaving the console with no idea it was ever asked to
        /// install anything. So the queue is filtered before it is used.
        /// </summary>
        private static bool IsAlive(Socket Socket)
        {
            try
            {
                if (!Socket.Connected)
                    return false;

                //Poll also reports readable on a half closed connection, so an empty
                //buffer means the peer is gone for good.
                return !(Socket.Poll(0, SelectMode.SelectRead) && Socket.Available == 0);
            }
            catch
            {
                return false;
            }
        }

        private void PruneDeadSockets()
        {
            if (Queue.Count == 0)
                return;

            var Alive = new List<Socket>();
            int Dropped = 0;

            while (Queue.Count > 0)
            {
                var Connection = Queue.Dequeue();

                if (IsAlive(Connection))
                    Alive.Add(Connection);
                else
                {
                    Dropped++;
                    try { Connection.Close(); } catch { }
                }
            }

            foreach (var Connection in Alive)
                Queue.Enqueue(Connection);

            ClientRunning = Queue.Count > 0;

            if (Dropped > 0)
                PS4Server.Trace("PAYLOAD: dropped {0} connection(s) the console had already closed", Dropped);
        }

        public async Task<bool> SendPKGPayload(string PS4IP, string PCIP, string URL, bool Silent, bool AutoSplit)
        {
            if (Installer.Server == null)
            {
                PS4Server.Trace("PAYLOAD: the HTTP server isn't running, nothing was sent to the console");
                return false;
            }

            URL = Installer.Server.RegisterJSON(URL, PCIP, Installer.CurrentPKG, AutoSplit);

            PS4Server.Trace("PAYLOAD: sending '{0}' ({1}, {2} bytes) to {3}, split: {4}",
                Installer.CurrentPKG.FriendlyName, Installer.CurrentPKG.ContentID, Installer.CurrentPKG.PackageSize, PS4IP, AutoSplit);

            if (!await EnsureServer(PCIP, PS4IP))
            {
                PS4Server.Trace("PAYLOAD: failed to open the PKG info server, the console never connected back");
                return false;
            }

            //Before deciding whether the console is still listening, get rid of the
            //connections it has already closed.
            PruneDeadSockets();

            Socket? PKGInfoSocket = null;

            //A console that is alive but drops us at the wrong moment is worth a second
            //try, the payload is only sent when nothing is queued so this re-injects it.
            for (int Attempt = 0; Attempt < 2 && PKGInfoSocket == null; Attempt++)
            {
                if (Attempt > 0)
                {
                    PS4Server.Trace("PAYLOAD: retrying, the connection died mid handoff");

                    PruneDeadSockets();
                }

                //Runs on every attempt, it only injects the payload when nothing live
                //is queued, so it stays a no-op when the console is already waiting.
                if (!EnsureClient(PCIP))
                {
                    PS4Server.Trace("PAYLOAD: failed to send the installer payload to {0}, the console isn't running it", PS4IP);
                    return false;
                }

                DateTime WaitBegin = DateTime.Now;
                while (Queue.Count == 0 && (DateTime.Now - WaitBegin).TotalSeconds < 10)
                {
                    if (!ServerRunning)
                        await EnsureServer(PCIP, PS4IP);

                    await Task.Delay(100);
                }

                if (Queue.Count == 0)
                {
                    PS4Server.Trace("PAYLOAD: the console didn't ask for the package within 10s, {0} never connected", PS4IP);

                    ServiceSocket?.Dispose();
                    ServiceSocket = null;
                    ServerRunning = false;
                    return false;
                }

                var Candidate = Queue.Dequeue();

                if (IsAlive(Candidate))
                    PKGInfoSocket = Candidate;
                else
                {
                    PS4Server.Trace("PAYLOAD: that connection was already closed by the console");
                    try { Candidate.Close(); } catch { }
                }
            }

            if (PKGInfoSocket == null)
            {
                PS4Server.Trace("PAYLOAD: no usable connection to the console, the package wasn't handed over");
                return false;
            }

            ClientRunning = Queue.Count > 0;

            long RequestsBefore = Installer.Server.TotalRequests;

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

            TaskCompletionSource SendTask = new TaskCompletionSource();

            SocketAsyncEventArgs PkgInfoEvent = new SocketAsyncEventArgs();
            PkgInfoEvent.RemoteEndPoint = PKGInfoSocket.RemoteEndPoint;
            PkgInfoEvent.SetBuffer(PKGInfoBuffer.ToArray());
            PkgInfoEvent.Completed += (sender, e) => ReturnToQueue(PKGInfoSocket, PkgInfoEvent, SendTask);

            if (!PKGInfoSocket.SendAsync(PkgInfoEvent))
            {
                ReturnToQueue(PKGInfoSocket, PkgInfoEvent, SendTask);
            }

            await SendTask.Task;

            PS4Server.Trace("PAYLOAD: package info handed over, waiting for the console to request the file");

            //Handing the URL over is not proof of anything, the console can drop the
            //connection right after. Give it a moment to show up at the server, that's
            //the only real confirmation that it started downloading.
            const int ConfirmTimeoutMs = 6000;

            DateTime ConfirmBegin = DateTime.Now;

            while (Installer.Server!.TotalRequests == RequestsBefore && (DateTime.Now - ConfirmBegin).TotalMilliseconds < ConfirmTimeoutMs)
                await Task.Delay(100);

            if (Installer.Server.TotalRequests == RequestsBefore)
            {
                PS4Server.Trace("PAYLOAD: the console never requested the file, '{0}' is not being installed", Installer.CurrentPKG.FriendlyName);

                if (!Silent)
                    await MessageBox.ShowAsync(Language.L("Msg.PackageNotRequested"), "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                return false;
            }

            PS4Server.Trace("PAYLOAD: the console requested the file, the download has started");

            if (!Silent)
                await MessageBox.ShowAsync(Language.L("Msg.PackageSent"), "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Information);

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
                    ConnectionEvent.Completed += (sender, e) => CloseAndDispose(Connection, ConnectionEvent, Source);

                    if (!Connection.SendAsync(ConnectionEvent))
                    {
                        CloseAndDispose(Connection, ConnectionEvent, Source);
                    }
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
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.ReceiveTimeout = 3000;
                socket.SendTimeout = 3000;
                socket.NoDelay = true;

                using CancellationTokenSource CToken = new CancellationTokenSource();
                CToken.CancelAfter(3000);

                try
                {
                    var Endpoint = new IPEndPoint(IPAddress.Parse(IP), Port);

                    if (App.IsAndroid)
                        socket.Connect(Endpoint);
                    else
                        await socket.ConnectAsync(Endpoint, CToken.Token);

                    if (socket.Connected)
                    {
                        PayloadSocket = socket;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    PS4Server.Trace("PAYLOAD: port {0} didn't answer: {1}", Port, PS4Server.Error(ex));
                }

                socket.Dispose();
            }

            if (Retry)
            {
                await Task.Delay(3000);
                return await TryConnectSocket(IP, false);
            }

            PS4Server.Trace("PAYLOAD: no payload port open on {0}, the console isn't running GoldHEN/MiraLoader or etaHEN", IP);

            return false;
        }

        /// <summary>
        /// Ensure the PKG Info Server is Listening
        /// </summary>
        /// <param name="PS4IP"></param>
        /// <returns></returns>
        private async Task<bool> EnsureServer(string PCIP, string PS4IP)
        {
            try
            {
                if (!ServerRunning)
                {
                    if (ServiceSocket == null)
                    {
                        ServiceSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        ServiceSocket.NoDelay = true;

                        if (App.IsAndroid)
                            ServiceSocket.Bind(new IPEndPoint(IPAddress.Parse(PCIP), App.Config.PayloadPort ?? 0));
                        else
                            ServiceSocket.Bind(new IPEndPoint(IPAddress.Any, App.Config.PayloadPort ?? 0));

                        ServiceSocket.Listen();
                    }


                    ServerRunning = true;

                    _ = ServerLoop();
                }

                if (PayloadSocket == null || !PayloadSocket.Connected)
                {
                    if (!await TryConnectSocket(PS4IP))
                        return false;
                }
            }
            catch (Exception ex)
            {
                PS4Server.Trace("PAYLOAD: PKG info server failed: {0}", PS4Server.Error(ex));
                throw;
            }
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
                    if (App.IsAndroid)
                    {
                        var ClientSocket = await AcceptConnectionInBackground(CToken.Token);
                        if (ClientSocket == null) throw new NullReferenceException();
                        ClientSocket.NoDelay = true;
                        Queue.Enqueue(ClientSocket);
                    }
                    else
                    {
                        var ClientSocket = await ServiceSocket.AcceptAsync(CToken.Token);
                        if (ClientSocket == null) throw new NullReferenceException();
                        ClientSocket.NoDelay = true;
                        Queue.Enqueue(ClientSocket);
                    }
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

        private async Task<Socket?> AcceptConnectionInBackground(CancellationToken Token)
        {
            Socket? ClientSocket = null;

            var thread = new Thread(() =>
            {
                ClientSocket = ServiceSocket.Accept();
            });

            thread.IsBackground = true;
            thread.Start();

            while (thread.IsAlive && !Token.IsCancellationRequested)
            {
                await Task.Delay(100);
            }

            try
            {
                if (thread.IsAlive)
                    thread.Interrupt();
            }
            catch { }

            if (ClientSocket == null)
                throw new NullReferenceException(nameof(ClientSocket));

            return ClientSocket;
        }

        /// <summary>
        /// Ensure the Client Installer Payload is running in the PS4 System
        /// </summary>
        /// <param name="PCIP">The PC IP</param>
        /// <returns>If is running/started returns true, otherwise false</returns>
        private bool EnsureClient(string PCIP)
        {
            //Only a live queued connection proves the console is still listening. The
            //ClientRunning flag used to be used here, but it stays true after the
            //console closes the connection, which left the second package of a batch
            //with the payload never sent and no error reported.
            if (Queue.Count > 0)
                return true;

            if (ServiceSocket == null)
            {
                PS4Server.Trace("PAYLOAD: the PKG info socket isn't open");
                return false;
            }

            if (PayloadSocket == null)
            {
                PS4Server.Trace("PAYLOAD: the payload port socket isn't connected");
                return false;
            }

            var Payload = Resources.Payload;

            var Offset = Payload.IndexOf(new byte[] { 0xB4, 0xB4, 0xB4, 0xB4, 0xB4, 0xB4 });
            if (Offset == -1)
            {
                PS4Server.Trace("PAYLOAD: the payload resource doesn't match this build, the console can't be started");
                return false;
            }

            try
            {
                ushort LocalPort = (ushort)((IPEndPoint)ServiceSocket.LocalEndPoint!).Port;

                var IP = IPAddress.Parse(PCIP).GetAddressBytes();
                var Port = BitConverter.GetBytes(LocalPort).Reverse().ToArray();

                IP.CopyTo(Payload, Offset);
                Port.CopyTo(Payload, Offset + 4);

                PayloadSocket.SendBufferSize = Payload.Length;

                if (PayloadSocket.Send(Payload) != Payload.Length)
                {
                    PS4Server.Trace("PAYLOAD: the installer payload wasn't sent completely");
                    return false;
                }

                PS4Server.Trace("PAYLOAD: installer payload sent, waiting for the console to connect back to {0}:{1}", PCIP, LocalPort);
            }
            catch (Exception ex)
            {
                PS4Server.Trace("PAYLOAD: failed to send the installer payload: {0}", PS4Server.Error(ex));
                return false;
            }
            finally
            {
                if (PayloadSocket != null)
                {
                    try { PayloadSocket.Shutdown(SocketShutdown.Both); } catch { }
                    PayloadSocket.Close();
                    PayloadSocket = null;
                }
            }

            return true;
        }

        private void ReturnToQueue(Socket socket, SocketAsyncEventArgs e, TaskCompletionSource? tcs = null)
        {
            if (socket.Connected)
            {
                Queue.Enqueue(socket);
                ClientRunning = true;
            }
            else
            {
                socket.Close();
                ClientRunning = Queue.Count > 0;
            }

            e.Dispose();

            if (tcs != null)
                tcs.TrySetResult();
        }

        private void CloseAndDispose(Socket socket, SocketAsyncEventArgs e, TaskCompletionSource? tcs = null)
        {
            try
            {
                if (socket.Connected)
                    socket.Shutdown(SocketShutdown.Both);
            }
            catch { }

            socket.Close();
            e.Dispose();

            if (tcs != null)
                tcs.TrySetResult();
        }
    }
}
