using System;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;

namespace Sample.Consumer
{
	public class TestBeforeInterceptor1 : IHorseInterceptor
	{
		public Task Intercept(HorseMessage message, HorseClient client)
		{
			_ = Console.Out.WriteLineAsync("BEFORE INTERCEPTOR 1");
			return Task.CompletedTask;
		}
	}

	public class TestBeforeInterceptor2 : IHorseInterceptor
	{
		public Task Intercept(HorseMessage message, HorseClient client)
		{
			_ = Console.Out.WriteLineAsync("BEFORE INTERCEPTOR 2");
			return Task.CompletedTask;
		}
	}
	
	public class TestAfterInterceptor1 : IHorseInterceptor
	{
		public Task Intercept(HorseMessage message, HorseClient client)
		{
			_ = Console.Out.WriteLineAsync("AFTER INTERCEPTOR 1");
			return Task.CompletedTask;
		}
	}
	
	public class TestAfterInterceptor2 : IHorseInterceptor
	{
		public Task Intercept(HorseMessage message, HorseClient client)
		{
			_ = Console.Out.WriteLineAsync("AFTER INTERCEPTOR 2");
			return Task.CompletedTask;
		}
	}
}