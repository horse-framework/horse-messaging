using System;
using System.Threading.Tasks;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Clients;

namespace Sample.Server;

public class ClientHandler : IClientHandler
{
    public Task Connected(HorseRider server, MessagingClient client)
    {
        Console.WriteLine("Client connected");
        return Task.CompletedTask;
    }

    public Task Disconnected(HorseRider server, MessagingClient client)
    {
        Console.WriteLine("Client disconnected");
        return Task.CompletedTask;
    }
}