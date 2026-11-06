using EnumsNET;

namespace Horse.Messaging.Server.Channels;

public class ChannelConfiguration
{
    public string Name { get; set; }
    public string Topic { get; set; }
    public string Status { get; set; }
    public ulong MessageSizeLimit { get; set; }
    public int ClientLimit { get; set; }
    public bool AutoDestroy { get; set; }
    public bool SendLastMessageAsInitial { get; set; }

    /// <summary>
    /// Nullable for files saved by older versions, which do not have this value. Null keeps the default option.
    /// </summary>
    public int? AutoDestroyIdleSeconds { get; set; }

    public static ChannelConfiguration Create(HorseChannel channel)
    {
        return new ChannelConfiguration
        {
            Name = channel.Name,
            Status = channel.Status.AsString(EnumFormat.Description),
            Topic = channel.Topic,
            AutoDestroy = channel.Options.AutoDestroy,
            ClientLimit = channel.Options.ClientLimit,
            MessageSizeLimit = channel.Options.MessageSizeLimit,
            SendLastMessageAsInitial = channel.Options.SendLastMessageAsInitial,
            AutoDestroyIdleSeconds = channel.Options.AutoDestroyIdleSeconds
        };
    }
}