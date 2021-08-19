using Horse.Messaging.Client.Channels.Annotations;

namespace Benchmark.Channel.Publisher
{
    [ChannelName("channel")]
    public class ChannelModel
    {
        public string Foo { get; set; }
    }
}