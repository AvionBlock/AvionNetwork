using AvionNetwork.Interfaces;
using AvionNetwork.SystemPackets.cs;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace AvionNetwork
{
    public class AvionClient<TPacket> : IDisposable where TPacket : AVPacket
    {
        private const int MTU = 65507;
        private static Dictionary<SocketAddress, EndPointInfo> EndPointLookup = new Dictionary<SocketAddress, EndPointInfo>();
        private static IPEndPoint EndPointFactory = new IPEndPoint(IPAddress.Any, 0);

        public int ResendTime = 500; //500ms
        public int MaxRetries = 20; //20 Packets

        public readonly PacketRegistry<TPacket> AVPacketRegistry;
        private readonly List<PacketInfo> ReliabilityQueue;
        private readonly List<PacketInfo> ReceiveBuffer;
        private readonly ConcurrentQueue<PacketInfo> SendBuffer;

        private Socket UDPSocket;
        private int Timeout;
        private uint SessionId;
        private Task? SenderThread;
        private Task? ReceiverThread;
        private EndPoint RemoteEndPoint;
        private CancellationTokenSource? CancellationTokenSource;

        public SocketState ConnectionState { get; private set; }
        public long LastActive { get; private set; }
        public bool IsDisposed { get; protected set; }

        public delegate void SocketEvent(string? reason = null);
        public delegate void PacketEvent(PacketInfo packetInfo);

        public event SocketEvent? OnConnected;
        public event SocketEvent? OnDisconnected;
        public event PacketEvent? OnPacketSent;
        public event PacketEvent? OnPacketReceived;
        public event PacketEvent? OnPacketAcknowledged;

        public AvionClient(PacketRegistry<TPacket> packetRegistry)
        {
            AVPacketRegistry = packetRegistry;
            UDPSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            ReliabilityQueue = new List<PacketInfo>();
            ReceiveBuffer = new List<PacketInfo>();
            SendBuffer = new ConcurrentQueue<PacketInfo>();
            RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            OnPacketReceived += PacketReceived;

            AVPacketRegistry.RegisterPacket((byte)AVPacketId.Login, typeof(AVLogin));
            AVPacketRegistry.RegisterPacket((byte)AVPacketId.Logout, typeof(AVLogout));
            AVPacketRegistry.RegisterPacket((byte)AVPacketId.Ping, typeof(AVPing));
            AVPacketRegistry.RegisterPacket((byte)AVPacketId.Ack, typeof(AVAck));
        }

        public void Connect(string ip, ushort port, byte[]? metaData = null, int timeout = 8000)
        {
            if (ConnectionState is SocketState.Connecting or SocketState.Connected) return;
            ConnectionState = SocketState.Connecting;
            Timeout = timeout;
            LastActive = Environment.TickCount64;

            if (CancellationTokenSource?.IsCancellationRequested ?? true)
            {
                CancellationTokenSource?.Dispose();
                CancellationTokenSource = new CancellationTokenSource();
            }

            try
            {
                if (IPAddress.TryParse(ip, out var IP))
                {
                    RemoteEndPoint = new IPEndPoint(IP, port);
                }
                else if (ip == "localhost") //Scuffed
                {
                    RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, port);
                }
                else
                {
                    IPHostEntry hostEntry = Dns.GetHostEntry(ip);

                    //you might get more than one ip for a hostname since 
                    //DNS supports more than one record

                    if (hostEntry.AddressList.Length > 0)
                    {
                        RemoteEndPoint = new IPEndPoint(hostEntry.AddressList[0], port);
                    }
                    else
                    {
                        throw new Exception("Could not resolve DNS hostname.");
                    }
                }

                UDPSocket.Bind(new IPEndPoint(IPAddress.Any, 0));
                ReceiverThread = Task.Run(() => ReceiverLogic(CancellationTokenSource.Token));
                SenderThread = Task.Run(() => SenderLogic(CancellationTokenSource.Token));
                Send(new AVLogin(metaData));
            }
            catch (Exception ex)
            {
                Disconnect(ex.Message);
            }
        }

        public void Disconnect(string? reason = null, bool waitForQueue = false)
        {
            if (waitForQueue)
            {
                while (ReliabilityQueue.Count > 0)
                    Task.Delay(1).Wait(); //prevent burning the CPU.
            }

            if (ConnectionState is SocketState.Connecting or SocketState.Connected)
            {
                ConnectionState = SocketState.Disconnected;
                UDPSocket.Close();
                UDPSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp); //Sockets are a bit of a pain.
                OnDisconnected?.Invoke(reason);
            }

            if (!CancellationTokenSource?.IsCancellationRequested ?? false)
                CancellationTokenSource?.Cancel();
            ReliabilityQueue.Clear();
            ReceiveBuffer.Clear();
        }

        public void Send(AVPacket packet)
        {
            var packetInfo = new PacketInfo(packet);

            if (ConnectionState != SocketState.Connected && !packet.Unconnected)
                throw new InvalidOperationException("The socket must be in a connected state in order to send connected packets!");
            if (packet.IsReliable)
                ReliabilityQueue.Add(packetInfo);

            SendBuffer.Enqueue(packetInfo);
        }

        public async Task SenderLogic(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (Environment.TickCount64 - LastActive >= Timeout)
                {
                    Disconnect("Timeout!"); //We can assume we cannot contact the server anymore.
                    break;
                }

                foreach (var packet in ReliabilityQueue)
                {
                    if (Environment.TickCount64 - packet.SentTime >= ResendTime)
                    {
                        packet.SentTime = Environment.TickCount64;
                        packet.Retries++;
                        packet.Packet.SessionId = SessionId; //Update this since it might've changed, but we may have not gotten the ack.
                        SendBuffer.Enqueue(packet);
                    }
                }

                while (SendBuffer.TryDequeue(out var packet))
                {
                    try
                    {
                        token.ThrowIfCancellationRequested();

                        if (packet.EndPoint == null)
                            packet.EndPoint = RemoteEndPoint;

                        byte[] buffer = GC.AllocateArray<byte>(length: MTU, pinned: true);
                        var written = packet.Packet.Write(ref buffer);
                        if (packet.EndPoint == null) //If endpoint is null, do not send.
                            continue;

                        var size = buffer.AsMemory().Slice(0, written);
                        await UDPSocket.SendToAsync(buffer.AsMemory().Slice(0, written), packet.EndPoint, token);

                        OnPacketSent?.Invoke(packet);
                    }
                    catch (SocketException ex)
                    {
                        //Socket exception, probably disconnected.
                        Disconnect(ex.Message);
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex); //Exception, Log it.
                    }
                }

                await Task.Delay(1).ConfigureAwait(false);
            }
        }

        public async Task ReceiverLogic(CancellationToken token)
        {
            byte[] buffer = GC.AllocateArray<byte>(length: MTU, pinned: true);
            Memory<byte> bufferMem = buffer.AsMemory();
            var receivedAddress = new SocketAddress(UDPSocket.AddressFamily);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var received = await UDPSocket.ReceiveFromAsync(bufferMem, SocketFlags.None, RemoteEndPoint, token);

                    var endPoint = GetOrCreateEndPoint(receivedAddress);
                    OnPacketReceived?.Invoke(new PacketInfo(AVPacketRegistry.GetPacketFromDataStream(bufferMem.Span), endPoint));
                }
                catch (SocketException ex)
                {
                    //Socket exception, probably disconnected.
                    Disconnect(ex.Message);
                    break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex); //Log it, but continue with execution.
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (IsDisposed) return;

            if (disposing)
            {
                Disconnect();
                UDPSocket.Dispose();
                //Sender and receiver threads should close.
            }

            IsDisposed = true;
        }

        ~AvionClient()
        {
            Dispose(false);
        }

        private EndPoint GetOrCreateEndPoint(SocketAddress receivedAddress)
        {
            if (!EndPointLookup.TryGetValue(receivedAddress, out var endpoint))
            {
                // Create an EndPoint from the SocketAddress
                endpoint = new EndPointInfo(EndPointFactory.Create(receivedAddress));

                var lookupCopy = new SocketAddress(receivedAddress.Family, receivedAddress.Size);
                receivedAddress.Buffer.CopyTo(lookupCopy.Buffer);

                EndPointLookup[lookupCopy] = endpoint;
            }

            endpoint.LastUsed = DateTime.UtcNow;
            return endpoint.EndPoint;
        }

        private void PacketReceived(PacketInfo packetInfo)
        {
            LastActive = Environment.TickCount64;

            switch ((AVPacketId)packetInfo.Packet.PacketId)
            {
                case AVPacketId.Login:
                    var loginPacket = (AVLogin)packetInfo.Packet;
                    //Decrypt and check.
                    ConnectionState = SocketState.Connected;
                    Send(new AVAck(packetInfo.Packet.Sequence));
                    break;
                case AVPacketId.Logout:
                    var logoutPacket = (AVLogout)packetInfo.Packet;
                    break;
                case AVPacketId.Ack:
                    foreach (var packet in ReliabilityQueue)
                    {
                        if (packet.Packet.Sequence == packetInfo.Packet.Sequence)
                        {
                            ReliabilityQueue.Remove(packet);
                            OnPacketAcknowledged?.Invoke(packet);
                        }
                    }
                    break;
            }
        }

        private struct EndPointInfo
        {
            public readonly EndPoint EndPoint;
            public DateTime LastUsed;

            public EndPointInfo(EndPoint endPoint)
            {
                EndPoint = endPoint;
                LastUsed = DateTime.UtcNow;
            }
        }
    }
}
