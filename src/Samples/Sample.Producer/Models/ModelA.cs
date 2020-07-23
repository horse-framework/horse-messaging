using Twino.Client.TMQ.Annotations;

namespace Sample.Producer.Models
{
    [QueueId(100)]
    [ChannelName("model-a")]
    public class ModelA
    {
        public int No { get; set; }
        public string Name { get; set; }
    }
}