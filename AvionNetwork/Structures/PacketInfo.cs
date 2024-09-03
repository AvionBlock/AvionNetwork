using AvionNetwork.Interfaces;
using System.Net;

namespace AvionNetwork.Structures
{
    public struct PacketInfo
    {
        public readonly AVPacket Packet;
        public EndPoint? EndPoint;

        public PacketInfo(AVPacket packet, EndPoint? endPoint = null)
        {
            Packet = packet;
            EndPoint = endPoint;
        }
    }
}
