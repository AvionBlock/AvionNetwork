using AvionNetwork.Interfaces;

namespace AvionNetwork.SystemPackets.cs
{
    public class AVPing : AVPacket
    {
        public override byte PacketId => (byte)AVPacketId.Ping;
        public override bool IsReliable => false;
        public override bool Unconnected => false;
    }
}
