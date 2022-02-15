using System;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;
using HostedServiceSample.Producer;
using Newtonsoft.Json;

namespace HostedServiceSample.Consumer
{
    [AutoAck]
    [AutoNack(NegativeReason.Error)]
    internal class TestQueueModelConsumer : IQueueConsumer<TestQueueModel>
    {
        public Task Consume(HorseMessage message, TestQueueModel model, HorseClient client)
        {
            _ = Console.Out.WriteLineAsync("Consumed!!!");
            _ = Console.Out.WriteLineAsync(JsonConvert.SerializeObject(model, Formatting.Indented));
            return Task.CompletedTask;
        }
    }

    [AutoAck]
    [AutoNack(NegativeReason.Error)]
    internal class TestQueueModel2Consumer : IQueueConsumer<TestQueueModel2>
    {
        public Task Consume(HorseMessage message, TestQueueModel2 model, HorseClient client)
        {
            _ = Console.Out.WriteLineAsync("Consumed!!!");
            _ = Console.Out.WriteLineAsync(JsonConvert.SerializeObject(model, Formatting.Indented));
            return Task.CompletedTask;
        }
    }
}