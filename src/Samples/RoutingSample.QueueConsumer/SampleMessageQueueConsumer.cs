using System;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;
using RoutingSample.Models;
using Horse.Mq.Client;
using Horse.Mq.Client.Annotations;

namespace RoutingSample.QueueConsumer
{
	[AutoAck]
	[AutoNack]
	public class SampleMessageQueueConsumer : IQueueConsumer<SampleMessage>
	{
		public Task Consume(HorseMessage message, SampleMessage model, HorseClient client)
		{
			Console.WriteLine("SAMPLE QUEUE MESSAGE CONSUMED");
			return Task.CompletedTask;
		}
	}
}