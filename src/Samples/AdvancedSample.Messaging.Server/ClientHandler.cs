using System;
using System.Threading.Tasks;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Clients;

namespace Sample.Server
{
	internal class ClientHandler : IClientHandler
	{
		public Task Connected(HorseRider server, MessagingClient client)
		{
			_ = Console.Out.WriteLineAsync($"Client connected [{client.Type}]");
			return Task.CompletedTask;
		}

		public Task Disconnected(HorseRider server, MessagingClient client)
		{
			_ = Console.Out.WriteLineAsync($"Client disconnected [{client.Type}]");
			return Task.CompletedTask;
		}
	}
}