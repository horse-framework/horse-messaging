using Twino.Client.TMQ.Annotations;

namespace Sample.Consumer.Models
{
    [QueueId(200)]
    [ChannelName("model-b")]
    public class ModelB
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
    }
}