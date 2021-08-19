using Horse.Messaging.Client.Channels.Annotations;

namespace Benchmark.Channel.Subscriber
{
    [ChannelName("channel")]
    public class ChannelModel
    {
        public string Foo { get; set; }
    }
}