using AvionNetwork.Interfaces;
using System.Collections.Concurrent;

namespace AvionNetwork
{
    public class PacketRegistry<TPacket> where TPacket : AVPacket
    {
        private ConcurrentDictionary<byte, Type> RegisteredPackets = new ConcurrentDictionary<byte, Type>();

        /// <summary>
        /// Registers a packet.
        /// </summary>
        /// <param name="id">The Id of the packet.</param>
        /// <param name="packetType">The type to create for the data to be parsed.</param>
        public void RegisterPacket(byte id, Type packetType)
        {
            if (!typeof(TPacket).IsAssignableFrom(packetType))
                throw new ArgumentException($"PacketType needs to inherit from {typeof(TPacket).Name}.", nameof(packetType));

            RegisteredPackets.AddOrUpdate(id, packetType, (key, old) => old = packetType);
        }

        /// <summary>
        /// Deregisters a packet.
        /// </summary>
        /// <param name="id">The Id of the packet.</param>
        /// <returns>The deregistered packet type.</returns>
        public Type? DeregisterPacket(byte id)
        {
            if (RegisteredPackets.TryRemove(id, out var packet)) return packet;
            return null;
        }

        /// <summary>
        /// Deregisters all registered packets.
        /// </summary>
        public void DeregisterAll()
        {
            RegisteredPackets.Clear();
        }

        /// <summary>
        /// Converts a packet from a byte array to the object.
        /// </summary>
        /// <param name="dataStream">The raw data.</param>
        /// <returns>The packet.</returns>
        /// <exception cref="InvalidOperationException"></exception>
        public TPacket GetPacketFromDataStream(Span<byte> dataStream)
        {
            var packetId = AVPacket.ReadId(ref dataStream); //This is the ID.

            if (!RegisteredPackets.TryGetValue(packetId, out var packetType))
                throw new InvalidOperationException($"Invalid packet id {packetId}.");

            TPacket packet = GetPacketFromType(packetType);
            packet.Read(ref dataStream);

            return packet;
        }

        /// <summary>
        /// Create's a packet from the type.
        /// </summary>
        /// <param name="packetType">The packet type.</param>
        /// <returns>The packet</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="Exception"></exception>
        public static TPacket GetPacketFromType(Type packetType)
        {
            if (!typeof(TPacket).IsAssignableFrom(packetType))
                throw new ArgumentException($"PacketType needs to inherit from {typeof(TPacket).Name}.", nameof(packetType));

            var packet = Activator.CreateInstance(packetType);
            if (packet == null) throw new Exception("Could not create packet instance.");

            return (TPacket)packet;
        }
    }
}
