namespace Horse.Messaging.Server.Channels;

internal class ChannelConfigData
{
    public string Name { get; set; }
    public string Topic { get; set; }
    public string Status { get; set; }
    public ulong MessageSizeLimit { get; set; }
    public int ClientLimit { get; set; }
    public bool AutoDestroy { get; set; }
}