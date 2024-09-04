using AvionNetwork.Interfaces;

namespace AvionNetwork.SystemPackets.cs
{
    public class AVLogin : AVPacket
    {
        public override byte PacketId => 0;
        public override bool IsReliable => true;
        public override bool Unconnected => true;

        public byte[] MetaData;

        public AVLogin(byte[]? metaData = null)
        {
            MetaData = metaData ?? Array.Empty<byte>();
        }

        public override int Write(ref byte[] dataStream, int offset = 0)
        {
            offset = base.Write(ref dataStream, offset);

            Buffer.BlockCopy(MetaData, 0, dataStream, offset, MetaData.Length);
            offset += MetaData.Length;

            return offset;
        }

        public override int Read(ref Span<byte> dataStream, int offset = 0)
        {
            offset = base.Read(ref dataStream, offset);

            MetaData = dataStream.Slice(offset, dataStream.Length - offset).ToArray();
            offset += MetaData.Length;

            return offset;
        }
    }
}
