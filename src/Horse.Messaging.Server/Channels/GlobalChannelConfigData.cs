using System.Collections.Generic;

namespace Horse.Messaging.Server.Channels;

internal class GlobalChannelConfigData
{
    public ulong MessageSizeLimit { get; set; }
    public int ClientLimit { get; set; }
    public bool AutoDestroy { get; set; }
    public bool AutoChannelCreation { get; set; }

    public List<ChannelConfigData> Channels { get; set; }
}