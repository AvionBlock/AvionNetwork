using AvionNetwork.Interfaces;
using System.Text;

namespace AvionNetwork.SystemPackets.cs
{
    public class AVLogout : AVPacket
    {
        public override byte PacketId => (byte)AVPacketId.Logout;
        public override bool IsReliable => false;
        public override bool Unconnected => false;

        public string Reason = string.Empty;

        public AVLogout(string? reason = null)
        {
            Reason = reason ?? string.Empty;
        }

        public override int Write(ref byte[] dataStream, int offset = 0)
        {
            offset = base.Write(ref dataStream, offset);

            Buffer.BlockCopy(BitConverter.GetBytes(Reason.Length), 0, dataStream, offset, sizeof(int));
            offset += sizeof(int);

            if (Reason.Length > 0)
            {
                var encodedString = Encoding.UTF8.GetBytes(Reason);
                Buffer.BlockCopy(encodedString, 0, dataStream, offset, encodedString.Length);
                offset += encodedString.Length;
            }

            return offset;
        }

        public override int Read(ref Span<byte> dataStream, int offset = 0)
        {
            offset = base.Read(ref dataStream, offset);

            var reasonLength = BitConverter.ToInt32(dataStream.Slice(offset, sizeof(int)));
            offset += sizeof(int);

            if (reasonLength > 0)
            {
                Encoding.UTF8.GetString(dataStream.Slice(offset, reasonLength));
                offset += reasonLength;
            }

            return offset;
        }
    }
}