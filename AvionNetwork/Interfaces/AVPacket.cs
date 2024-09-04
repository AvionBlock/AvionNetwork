namespace AvionNetwork.Interfaces
{
    public abstract class AVPacket
    {
        public abstract byte PacketId { get; }
        public abstract bool IsReliable { get; }
        public abstract bool Unconnected { get; }

        public uint SessionId;
        public uint Sequence;

        /// <summary>
        /// Writes into a data stream.
        /// </summary>
        /// <param name="dataStream">The dataStream to write to.</param>
        /// <returns>The amount written to the dataStream.</returns>
        public virtual int Write(ref byte[] dataStream, int offset = 0)
        {
            Array.Clear(dataStream); //Clear the packet stream.

            dataStream[0] = PacketId;
            offset++;

            if (!Unconnected)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(SessionId), 0, dataStream, offset, sizeof(uint));
                offset += sizeof(uint);
            }

            if (IsReliable)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(Sequence), 0, dataStream, offset, sizeof(uint));
                offset += sizeof(uint);
            }

            return offset;
        }

        /// <summary>
        /// Reads and converts a data stream into the packet.
        /// </summary>
        /// <param name="dataStream">The data stream.</param>
        /// <param name="offset">The offset to read from.</param>
        /// <returns>The amount of data read.</returns>
        public virtual int Read(ref Span<byte> dataStream, int offset = 0)
        {
            SessionId = BitConverter.ToUInt32(dataStream.Slice(offset, sizeof(uint)));
            offset += sizeof(uint);

            if (IsReliable)
            {
                Sequence = BitConverter.ToUInt32(dataStream.Slice(offset, sizeof(uint)));
                offset += sizeof(uint);
            }
            return offset; //Returns the amount of data read.
        }

        /// <summary>
        /// Reads the Id of a dataStream.
        /// </summary>
        /// <param name="data">The data to read.</param>
        /// <returns>A packet Id.</returns>
        public static byte ReadId(ref Span<byte> data)
        {
            return data[0];
        }
    }
}
