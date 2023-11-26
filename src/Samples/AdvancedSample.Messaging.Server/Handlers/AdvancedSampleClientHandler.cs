using System.Threading.Tasks;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Clients;
using Microsoft.Extensions.Logging;

namespace AdvancedSample.Messaging.Server.Handlers;

internal class AdvancedSampleClientHandler : IClientHandler
{
    private readonly ILogger<AdvancedSampleClientHandler> _logger;

    public AdvancedSampleClientHandler(ILogger<AdvancedSampleClientHandler> logger)
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