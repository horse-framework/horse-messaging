using System;
using System.Text.Json;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;
using HostedServiceSample.Producer;

namespace HostedServiceSample.Consumer
{
    [AutoAck]
    [AutoNack(NegativeReason.Error)]
    internal class TestQueueModelConsumer : IQueueConsumer<TestQueueModel>
    {
        readonly JsonSerializerOptions _options = new JsonSerializerOptions()
        {
            WriteIndented = true
        };

        public Task Consume(HorseMessage message, TestQueueModel model, HorseClient client)
        {
            _ = Console.Out.WriteLineAsync("Consumed!!!");
            _ = Console.Out.WriteLineAsync(JsonSerializer.Serialize(model));
            return Task.CompletedTask;
        }
    }

    [AutoAck]
    [AutoNack(NegativeReason.Error)]
    internal class TestQueueModel2Consumer : IQueueConsumer<TestQueueModel2>
    {
        readonly JsonSerializerOptions _options = new JsonSerializerOptions()
        {
            WriteIndented = true
        };

        public Task Consume(HorseMessage message, TestQueueModel2 model, HorseClient client)
        {
            _ = Console.Out.WriteLineAsync("Consumed!!!");
            _ = Console.Out.WriteLineAsync(JsonSerializer.Serialize(model));
            return Task.CompletedTask;
        }
    }
}