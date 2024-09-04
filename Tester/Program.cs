using AvionNetwork;
using AvionNetwork.Interfaces;

var avClient = new AvionClient<AVPacket>(new PacketRegistry<AVPacket>());

avClient.Connect("127.0.0.1", 9050);
Task.Delay(1000).Wait();
avClient.Connect("192.0.0.1", 9050);
Console.ReadLine();