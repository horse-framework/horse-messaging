using System.Collections.Generic;
using Twino.MQ.Channels;
using Twino.MQ.Clients;
using Twino.MQ.Helpers;
using Twino.MQ.Options;

namespace Twino.MQ
{
    public class MQServer
    {
        public ServerOptions Options { get; }
        
        private readonly FlexArray<Channel> _channels;
        public IEnumerable<Channel> Channels => _channels.All();

        private readonly FlexArray<MqClient> _clients;
        public IEnumerable<MqClient> Clients => _clients.All();

        public MQServer(ServerOptions options)
        {
            Options = options;
            _channels = new FlexArray<Channel>(options.ChannelCapacity);
            _clients = new FlexArray<MqClient>(options.ClientCapacity);
        }
    }
}