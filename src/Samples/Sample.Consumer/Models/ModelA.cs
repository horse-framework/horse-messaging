using Twino.Client.TMQ.Annotations;

namespace Sample.Consumer.Models
{
    [QueueId(100)]
    [ChannelName("model-a")]
    public class ModelA
    {
        public string Foo { get; set; }
    }
}