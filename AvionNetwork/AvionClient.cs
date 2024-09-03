using AvionNetwork.Interfaces;
using AvionNetwork.Structures;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace AvionNetwork
{
    public class AvionClient<TPacket> where TPacket : AVPacket
    {
        public AvionSocket<TPacket> Socket { get; set; }

        private readonly Channel<PacketInfo> ReliabilityQueue;
        private readonly Channel<PacketInfo> ReceiveBuffer;

		public AvionClient(PacketRegistry<TPacket> packetRegistry, CancellationToken cancellationToken = default)
        {
            Socket = new AvionSocket<TPacket>(packetRegistry, cancellationToken);
            ReliabilityQueue = Channel.CreateUnbounded<PacketInfo>();
            ReceiveBuffer = Channel.CreateUnbounded<PacketInfo>();
		}

        public void Connect(string ip, ushort port)
        {
            Socket.Connect(ip, port);


        }

        public void Send(AVPacket packet)
        {
            ReliabilityQueue.Writer.TryWrite(new PacketInfo(, packet));
        }
    }
}
