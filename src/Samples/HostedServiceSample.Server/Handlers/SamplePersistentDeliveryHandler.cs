using System;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Data;
using Horse.Messaging.Data.Configuration;
using Horse.Messaging.Data.Implementation;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Managers;

namespace HostedServiceSample.Server.Handlers
{
	public class SamplePersistentQueueManager: PersistentQueueManager
	{
		private static readonly DataConfigurationBuilder _builder = new();

		public SamplePersistentQueueManager(HorseQueue queue, DatabaseOptions options, bool useRedelivery = false):
			base(queue, options, useRedelivery)
		{
			DeliveryHandler = new SamplePersistentDeliveryHandler(this);
		}
	}

	public class SamplePersistentDeliveryHandler: PersistentMessageDeliveryHandler
	{
		public SamplePersistentDeliveryHandler(PersistentQueueManager manager): base(manager) { }

		public override Task<bool> CanConsumerReceive(HorseQueue queue, QueueMessage message, MessagingClient receiver)
		{
			string userIdParam = message.Message.FindHeader("UserId");
			userIdParam = string.IsNullOrEmpty(userIdParam) ? "0" : userIdParam;
			int messageOwner = int.Parse(userIdParam);
			bool hasDebugClient = messageOwner > 0 && queue.ClientsClone.Any(m => int.Parse(m.Client.Name) == messageOwner);
			if (hasDebugClient) return Task.FromResult(receiver.Name == messageOwner.ToString());
			return Task.FromResult(receiver.Name == "0");
		}
	}
}