using AvionNetwork.Interfaces;

namespace AvionNetwork.SystemPackets.cs
{
    public class AVAck : AVPacket
    {
        public override byte PacketId => (byte)AVPacketId.Ack;
        public override bool IsReliable => false;
        public override bool Unconnected => false;

        public AVAck(uint sequence)
        {
            Sequence = sequence;
        }
    }
}
