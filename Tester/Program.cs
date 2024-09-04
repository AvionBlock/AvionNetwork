using AvionNetwork;
using AvionNetwork.Interfaces;

var avClient = new AvionClient<AVPacket>(new PacketRegistry<AVPacket>());
avClient.OnPacketSent += AvClient_OnPacketSent;

void AvClient_OnPacketSent(PacketInfo packetInfo)
{
    Console.WriteLine(packetInfo.Retries);
}

avClient.Connect("127.0.0.1", 9050);
avClient.Connect("192.0.0.1", 9050);
Console.ReadLine();