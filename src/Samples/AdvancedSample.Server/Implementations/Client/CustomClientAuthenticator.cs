using Horse.Messaging.Server;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Security;

namespace AdvancedSample.Server.Implementations.Client;

public class CustomClientAuthenticator : IClientAuthenticator
{
    public string SymmetricKey { get; set; }
    public ILogger Logger { get; set; }
    private bool _considerToken;

    public CustomClientAuthenticator(ILogger logger, string symmetricKey, bool considerToken)
    {
        SymmetricKey = symmetricKey;
        Logger = logger;
        _considerToken = considerToken;
    }

    public async Task<bool> Authenticate(HorseRider server, MessagingClient client)
    {
        return _considerToken ? await Task.Run(() =>
        {
            var validate = SymmetricKey.ValidateAccessToken(client.Token, "AmqAccess", "True");
            if (validate.Key)
            {
                Logger.LogInformation($"client {client.Name} authenticated successfully.");
                return true;
            }

            return false;
        }) : await Task.Run(() =>
        {
            return true;
        });
    }
}