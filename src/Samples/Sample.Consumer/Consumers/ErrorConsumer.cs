using System;
using System.Threading.Tasks;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Annotations;
using Twino.Protocols.TMQ;

namespace Sample.Consumer.Consumers
{
    [QueueId(500)]
    [ChannelName("error")]
    public class ErrorConsumer : IQueueConsumer<string>
    {
        public Task Consume(TmqMessage message, string model, TmqClient client)
        {
            Console.WriteLine("Error consumed");
            return Task.CompletedTask;
        }
    }
}