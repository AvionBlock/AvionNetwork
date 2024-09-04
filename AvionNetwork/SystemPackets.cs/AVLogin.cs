using AvionNetwork.Interfaces;

namespace AvionNetwork.SystemPackets.cs
{
    public class AVLogin : AVPacket
    {
        public override byte PacketId => 0;

        public override bool IsReliable => true;

        public override bool Unconnected => true;
    }
}
