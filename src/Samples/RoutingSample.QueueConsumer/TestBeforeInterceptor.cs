using System;
using System.Threading.Tasks;
using Horse.Mq.Client;
using Horse.Protocols.Hmq;

namespace RoutingSample.QueueConsumer
{
	public interface IDenemeSession
	{
		public string Id { get;  }

		public void SetId(string id);
	}

	public class DenemeSession : IDenemeSession
	{
		public string Id { get; private set; }
		public void SetId(string id)
		{
			Id = id;
		}
	}
	public class TestBefore1Interceptor : IHorseMessageInterceptor
	{
		private readonly IDenemeSession _session;

		public TestBefore1Interceptor(IDenemeSession session)
		{
			_session = session;
		}
		public Task Intercept(HorseMessage message, HorseClient client)
		{
			_session.SetId(message.FindHeader("Id"));
			_ = Console.Out.WriteLineAsync($"INTERCEPT BEFORE 1 [{_session.Id}]");
			return Task.CompletedTask;
		}
	}

	public class TestBefore2Interceptor : IHorseMessageInterceptor
	{
		private readonly IDenemeSession _session;

		public TestBefore2Interceptor(IDenemeSession session)
		{
			_session = session;
		}
		public Task Intercept(HorseMessage message, HorseClient client)
		{
			_ = Console.Out.WriteLineAsync($"INTERCEPT BEFORE 2 [{_session.Id}]");
			return Task.CompletedTask;
		}
	}

	public class TestAfterInterceptor : IHorseMessageInterceptor
	{
		public Task Intercept(HorseMessage message, HorseClient client)
		{
			_ = Console.Out.WriteLineAsync("INTERCEPT AFTER");
			return Task.CompletedTask;
		}
	}
}