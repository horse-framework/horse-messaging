using System;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;
using Newtonsoft.Json;

namespace RoutingSample.ExceptionConsumer
{
    [QueueName("SAMPLE-EXCEPTION-QUEUE")]
    [QueueType(MessagingQueueType.Push)]
    [AutoAck]
    public class SampleExceptionConsumer : IQueueConsumer<string>
    {
        public Task Consume(HorseMessage message, string serializedException, HorseClient client,
            CancellationToken cancellationToken = default)
        {
            Exception exception = JsonConvert.DeserializeObject<Exception>(serializedException);
            Console.WriteLine(exception.Message);
            return Task.CompletedTask;
        }
    }
}