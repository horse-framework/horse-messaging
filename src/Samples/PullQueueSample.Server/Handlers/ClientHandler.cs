using Horse.Messaging.Server;
using Horse.Messaging.Server.Clients;
using Microsoft.Extensions.Logging;

namespace PullQueueSample.Server.Handlers;

internal class ClientHandler : IClientHandler
{
    private readonly ILogger<ClientHandler> _logger;

    public ClientHandler(ILogger<ClientHandler> logger)
    {
        _logger = logger;
    }

    public Task Connected(HorseRider server, MessagingClient client)
    {
        _logger.LogInformation("[CLIENT CONNECTED] {Type}", client.Type);
        return Task.CompletedTask;
    }

    public Task Disconnected(HorseRider server, MessagingClient client)
    {
        _logger.LogInformation("[CLIENT DISCONNECTED] {Type}", client.Type);
        return Task.CompletedTask;
    }
}