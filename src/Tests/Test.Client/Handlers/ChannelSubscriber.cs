using System;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Channels;
using Horse.Messaging.Client.Channels.Annotations;
using Horse.Messaging.Protocol;
using Test.Client.Models;

namespace Test.Client.Handlers;

[ChannelName("channel1")]
public class ChannelSubscriber : IChannelSubscriber<ModelA>
{
    public Task Handle(ModelA model, HorseMessage rawMessage, HorseClient client)
    {
        return Task.CompletedTask;
    }

    public Task Error(Exception exception, ModelA model, HorseMessage rawMessage, HorseClient client)
    {
        return Task.CompletedTask;
    }
}