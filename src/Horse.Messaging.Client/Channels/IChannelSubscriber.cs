using System;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Channels;

/// <summary>
/// Subscriber for horse channel
/// </summary>
public interface IChannelSubscriber<TModel>
{
    /// <summary>
    /// Triggered when a message is received from a channel
    /// </summary>
    Task Handle(ChannelMessageContext<TModel> context);

    /// <summary>
    /// Triggered when an exception is thrown while consuming the message
    /// </summary>
    Task Error(Exception exception, ChannelMessageContext<TModel> context);
}