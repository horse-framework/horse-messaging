using System;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;

namespace AdvancedSample.Core.Service
{
	public static class Extensions
	{
		public static void BuildHorseClient(this HorseClientBuilder builder, string hostname, string clientType)
		{
			builder.SetHost(hostname)
				   .SetClientType(clientType)
				   .SetReconnectWait(TimeSpan.FromSeconds(1))
				   .SetResponseTimeout(TimeSpan.FromSeconds(5))
				   .UseNewtonsoftJsonSerializer()
				   .OnConnected(OnConnected)
				   .OnDisconnected(OnDisconnected)
				   .OnMessageReceived(OnMessageReceived)
				   .OnError(OnError);
		}

		private static void OnError(Exception exception)
		{
			_ = Console.Out.WriteLineAsync(exception.Message);
		}

		private static void OnMessageReceived(HorseMessage message)
		{
			_ = Console.Out.WriteLineAsync($"[RECEIVED] {message}");
		}

		private static void OnDisconnected(HorseClient client)
		{
			_ = Console.Out.WriteLineAsync("DISCONNECTED!");
		}

		private static void OnConnected(HorseClient client)
		{
			_ = Console.Out.WriteLineAsync("CONNECTED!");
		}
	}
}