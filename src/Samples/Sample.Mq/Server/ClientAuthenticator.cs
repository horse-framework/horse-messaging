using System;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Security;

namespace Sample.Mq.Server
{
    public class ClientAuthenticator : IClientAuthenticator
    {
        public async Task<bool> Authenticate(Twino.MQ.MqServer server, MqClient client)
        {
            if (string.IsNullOrEmpty(client.Type))
                return false;

            if (string.IsNullOrEmpty(client.Token))
                return false;

            if (client.Type.Equals("producer", StringComparison.InvariantCultureIgnoreCase))
                return client.Token == "S3cr37_pr0duc3r_t0k3n";

            if (client.Type.Equals("consumer", StringComparison.InvariantCultureIgnoreCase))
                return client.Token == "anonymous";

            return await Task.FromResult(false);
        }
    }
}