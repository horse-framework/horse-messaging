using System;
using System.Threading.Tasks;
using RoutingSample.Models;
using Horse.Mq.Client;
using Horse.Mq.Client.Annotations;
using Horse.Protocols.Hmq;

namespace RoutingSample.QueueConsumer
{
	[AutoAck]
	[AutoNack]
	[HorseMessageInterceptor(typeof(TestBefore1Interceptor))]
	[HorseMessageInterceptor(typeof(TestBefore2Interceptor))]
	[HorseMessageInterceptor(typeof(TestAfterInterceptor),Intercept.After)]
	public class SampleMessageQueueConsumer : IQueueConsumer<SampleMessage>
	{
		private readonly IDenemeSession _session;

		public SampleMessageQueueConsumer(IDenemeSession session)
		{
			_session = session;
		}
		public Task Consume(HorseMessage message, SampleMessage model, HorseClient client)
		{
			Console.WriteLine($"SAMPLE QUEUE MESSAGE CONSUMED [{_session.Id}]");
			return Task.CompletedTask;
		}
	}
}