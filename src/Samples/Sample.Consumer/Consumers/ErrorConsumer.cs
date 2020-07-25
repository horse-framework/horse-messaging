using System;
using System.Threading.Tasks;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Annotations;
using Twino.Protocols.TMQ;

namespace Sample.Consumer.Consumers
{
    [QueueId(500)]
    [ChannelName("error")]
    public class ErrorConsumer1 : IQueueConsumer<string>
    {
        public Task Consume(TmqMessage message, string model, TmqClient client)
        {
            Console.WriteLine("Not Supported: " + model);
            return Task.CompletedTask;
        }
    }

    [QueueId(501)]
    [ChannelName("error")]
    public class ErrorConsumer2 : IQueueConsumer<string>
    {
        public Task Consume(TmqMessage message, string model, TmqClient client)
        {
            var e = Newtonsoft.Json.JsonConvert.DeserializeObject<ApplicationException>(model);
            Console.WriteLine("App Error: " + e);
            return Task.CompletedTask;
        }
    }
}