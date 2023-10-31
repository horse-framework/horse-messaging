using Horse.Messaging.Server;
using Horse.Messaging.Server.Clients;

namespace AdvancedSample.Server.Implementations.Client
{
    public class ClientHandler : IClientHandler
    {
        private readonly ILogger _logger;
        public ClientHandler(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        public Task Connected(HorseRider server, MessagingClient client)
        {
            _logger.LogInformation($"Client {client.Name} connected.");
            return Task.CompletedTask;
        }

        public Task Disconnected(HorseRider server, MessagingClient client)
        {
            _logger.LogInformation($"Client {client.Name} disConnected.");

            return Task.CompletedTask;
        }
    }
}
