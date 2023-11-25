using System;
using System.Threading.Tasks;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Clients;

namespace Test.Common.Handlers;

public class TestClientHandler : IClientHandler
{
    private readonly TestHorseRider _rider;

    public TestClientHandler(TestHorseRider rider)
    {
        _rider = rider;
    }

    public Task Connected(HorseRider server, MessagingClient client)
    {
        Console.WriteLine("Client Connected");
        _rider.ClientConnected++;
        return Task.CompletedTask;
    }

    public Task Disconnected(HorseRider server, MessagingClient client)
    {
        Console.WriteLine("Client Disconnected");
        _rider.ClientDisconnected++;
        return Task.CompletedTask;
    }
}