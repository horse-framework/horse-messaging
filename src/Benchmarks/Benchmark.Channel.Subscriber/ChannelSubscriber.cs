using System;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Channels;
using Horse.Messaging.Client.Channels.Annotations;
using Horse.Messaging.Protocol;

namespace Benchmark.Channel.Subscriber
{
    public class ChannelSubscriber : IChannelSubscriber<ChannelModel>
    {
        public Task Handle(ChannelModel model, HorseMessage rawMessage, HorseClient client)
        {
            Program.Counter.Increase();
            return Task.CompletedTask;
        }

        public async Task Error(Exception exception, ChannelModel model, HorseMessage rawMessage, HorseClient client)
        {
            throw new NotImplementedException();
        }
    }
}