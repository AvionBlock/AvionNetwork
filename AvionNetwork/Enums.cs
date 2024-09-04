namespace AvionNetwork
{
    public enum SocketState
    {
        Disconnected,
        Connecting,
        Connected
    }

    public enum AVPacketId : byte
    {
        Login,
        Logout,
        Ping,
        Ack
    }
}
