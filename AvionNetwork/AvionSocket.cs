using AvionNetwork.Interfaces;
using AvionNetwork.Structures;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace AvionNetwork
{
    public class AvionSocket<TPacket> where TPacket : AVPacket
    {
        private const int MTU = 65527;
        private static Dictionary<SocketAddress, EndPointInfo> EndPointLookup = new Dictionary<SocketAddress, EndPointInfo>();
        private static IPEndPoint EndPointFactory = new IPEndPoint(IPAddress.Any, 0);

        public readonly Socket UDPSocket;
        public readonly Channel<PacketInfo> SendBuffer;
        public readonly PacketRegistry<TPacket> PacketRegistry;
        public readonly CancellationToken CancellationToken;

        private Task? SenderThread;
        private Task? ReceiverThread;

        public delegate void PacketEvent(PacketInfo packetInfo);

        public event PacketEvent? OnPacketSent;
        public event PacketEvent? OnPacketReceived;

        public AvionSocket(PacketRegistry<TPacket> packetRegistry, CancellationToken cancellationToken = default)
        {
            UDPSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            SendBuffer = Channel.CreateUnbounded<PacketInfo>();
            PacketRegistry = packetRegistry;
            CancellationToken = cancellationToken;
        }

        public void Connect(string IP, ushort Port)
        {
            UDPSocket.Connect(IP, Port);

            SenderThread = Task.Run(SenderLogic);
            ReceiverThread = Task.Run(ReceiverLogic);
        }

        public void Listen(int port)
        {
            UDPSocket.Connect(IPAddress.Any, port);

            SenderThread = Task.Run(SenderLogic);
            ReceiverThread = Task.Run(ReceiverLogic);
        }

        public void Disconnect()
        {
            UDPSocket.Disconnect(true);
        }

        public void Send(PacketInfo packetInfo)
        {
            if(packetInfo.EndPoint == null && UDPSocket.RemoteEndPoint != null)
                packetInfo.EndPoint = UDPSocket.RemoteEndPoint;

            SendBuffer.Writer.TryWrite(packetInfo);
        }

        private async Task ReceiverLogic()
        {
            byte[] buffer = GC.AllocateArray<byte>(length: MTU, pinned: true);
            Memory<byte> bufferMem = buffer.AsMemory();
            var receivedAddress = new SocketAddress(UDPSocket.AddressFamily);

            while (UDPSocket.Connected || !CancellationToken.IsCancellationRequested)
            {
                try
                {
                    var received = await UDPSocket.ReceiveAsync(bufferMem, SocketFlags.None, CancellationToken);

                    var endPoint = GetOrCreateEndPoint(receivedAddress);
                    OnPacketReceived?.Invoke(new PacketInfo(PacketRegistry.GetPacketFromDataStream(bufferMem.Span), endPoint));
                }
                catch (SocketException)
                {
                    //Socket exception, probably disconnected.
                    break;
                }
            }
        }

        private async Task SenderLogic()
        {
            while (UDPSocket.Connected || !CancellationToken.IsCancellationRequested)
            {
                while (SendBuffer.Reader.TryRead(out var packet) && UDPSocket.Connected)
                {
                    byte[] buffer = GC.AllocateArray<byte>(length: MTU, pinned: true);
                    packet.Packet.Write(ref buffer);
                    if (packet.EndPoint == null) //If endpoint is null, do not send.
                        continue;

                    await UDPSocket.SendToAsync(buffer.AsMemory(), packet.EndPoint);

                    OnPacketSent?.Invoke(packet);
                }

                await Task.Delay(1); //Prevent burning the CPU.
            }
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
