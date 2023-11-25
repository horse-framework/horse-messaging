using Horse.Messaging.Client.Channels.Annotations;

namespace Test.Client.Models;

[ChannelName("overwritten-channel-name")]
public class ModelA
{
    public string Foo { get; set; }
}