using System;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;
using HostedServiceSample.Common;
using HostedServiceSample.Producer;
using Newtonsoft.Json;

namespace HostedServiceSample.Consumer;

[AutoAck]
[AutoNack(NegativeReason.Error)]
internal class SerializedExceptionConsumer: IQueueConsumer<SerializedException>
{
	public Task Consume(HorseMessage message, SerializedException model, HorseClient client)
	{
		_ = Console.Out.WriteLineAsync("Exception Consumed!!!");
		_ = Console.Out.WriteLineAsync(JsonConvert.SerializeObject(model, Formatting.Indented));
		return Task.CompletedTask;
	}
}