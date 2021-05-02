using System;
using System.Threading.Tasks;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;

namespace AdvancedSample.Messaging.Server
{
	internal class QueueEventHandler : IQueueEventHandler
	{
		public Task OnCreated(HorseQueue queue)
		{
			_ = Console.Out.WriteLineAsync($"Queue created: {queue.Name}");
			return Task.CompletedTask;
		}

		public Task OnRemoved(HorseQueue queue)
		{
			_ = Console.Out.WriteLineAsync($"Queue removed: {queue.Name}");
			return Task.CompletedTask;
		}

		public Task OnConsumerSubscribed(QueueClient client)
		{
			_ = Console.Out.WriteLineAsync($"Consumer subscribed to {client.Queue.Name}");
			return Task.CompletedTask;
		}

		public Task OnConsumerUnsubscribed(QueueClient client)
		{
			_ = Console.Out.WriteLineAsync($"Consumer unsubscribed from {client.Queue.Name}");
			return Task.CompletedTask;
		}

		public Task OnStatusChanged(HorseQueue queue, QueueStatus @from, QueueStatus to)
		{
			_ = Console.Out.WriteLineAsync($"{queue.Name} status changed from {@from} to {to}");
			return Task.CompletedTask;
		}
	}
}