using System;
using System.Text.Json;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;
using HostedServiceSample.Common;

namespace HostedServiceSample.Consumer;

[AutoAck]
[AutoNack(NegativeReason.Error)]
internal class SerializedExceptionConsumer: IQueueConsumer<SerializedException>
{
	readonly JsonSerializerOptions _options = new()
    {
		WriteIndented = true
	};

	public Task Consume(HorseMessage message, SerializedException model, HorseClient client)
	{
		_ = Console.Out.WriteLineAsync("Exception Consumed!!!");
		_ = Console.Out.WriteLineAsync(JsonSerializer.Serialize(model, _options));
		return Task.CompletedTask;
	}
}