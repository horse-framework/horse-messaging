using System;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Channels;
using Horse.Messaging.Protocol;

namespace Benchmark.Channel.Subscriber;

public class ChannelSubscriber : IChannelSubscriber<ChannelModel>
{
    public Task Handle(ChannelMessageContext<ChannelModel> context)
    {
        Program.Counter.Increase();
        return Task.CompletedTask;
    }

    public Task Error(Exception exception, ChannelMessageContext<ChannelModel> context)
    {
        throw new NotImplementedException();
    }
}
