using System;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;
using RoutingSample.Models;

namespace RoutingSample.QueueConsumer
{
	[AutoAck]
	[AutoNack]
	[HorseMessageInterceptor(typeof(TestBefore1Interceptor))]
	public abstract class SampleMessageQueueConsumerBase<T> : IQueueConsumer<T>
	{
		private readonly IDenemeSession _session;

		protected SampleMessageQueueConsumerBase(IDenemeSession session)
		{
			_session = session;
		}
		
		public Task Consume(HorseMessage message, T model, HorseClient client)
		{
			Console.WriteLine($"SAMPLE QUEUE MESSAGE CONSUMED BASE [{_session.Id}]");
			return Handle(model);
		}

		protected abstract Task Handle(T model);
	}	

	[HorseMessageInterceptor(typeof(TestBefore2Interceptor))]
	public class SampleMessageQueueConsumer : SampleMessageQueueConsumerBase<SampleMessage>
	{
		private readonly IDenemeSession _session;

		public SampleMessageQueueConsumer(IDenemeSession session):base(session)
		{
			_session = session;
		}
		protected override Task Handle(SampleMessage model)
		{
			Console.WriteLine($"SAMPLE QUEUE MESSAGE CONSUMED [{_session.Id}]");
			return Task.CompletedTask;
		}
	}
	
}