using System;
using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Clients;
using Twino.MQ.Security;

namespace Sample.MqServer
{
    public class ClientAuthenticator : IClientAuthenticator
    {
        public async Task<bool> Authenticate(MQServer server, MqClient client)
        {
            Console.WriteLine($"{client.UniqueId} authenticated in server");
            return await Task.FromResult(true);
        }
    }
}