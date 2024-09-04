using AvionNetwork.Interfaces;
using System.Net;

namespace AvionNetwork
{
    public class PacketInfo
    {
        public readonly AVPacket Packet;
        public EndPoint? EndPoint;
        public long SentTime;
        public uint Retries;

        public PacketInfo(AVPacket packet, EndPoint? endPoint = null)
        {
            Packet = packet;
            EndPoint = endPoint;
            SentTime = Environment.TickCount64;
            Retries = 0;
        }
    }
}
