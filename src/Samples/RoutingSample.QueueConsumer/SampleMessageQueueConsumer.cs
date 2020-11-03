using System;
using System.Threading.Tasks;
using RoutingSample.Models;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Annotations;
using Twino.Protocols.TMQ;

namespace RoutingSample.QueueConsumer
{
	[AutoAck]
	[AutoNack]
	public class SampleMessageQueueConsumer : IQueueConsumer<SampleMessage>
	{
		public Task Consume(TwinoMessage message, SampleMessage model, TmqClient client)
		{
			Console.WriteLine("SAMPLE QUEUE MESSAGE CONSUMED");
			return Task.CompletedTask;
		}
	}
}