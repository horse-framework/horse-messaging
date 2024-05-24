using System;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Channels;

/// <summary>
/// Subscriber for horse channel
/// </summary>
public interface IChannelSubscriber<in TModel>
{
    /// <summary>
    /// Triggered when a message is received from a channel
    /// </summary>
    /// <param name="model">Message model</param>
    /// <param name="rawMessage">Raw message</param>
    /// <param name="client">Consumer client</param>
    /// <returns></returns>
    Task Handle(TModel model, HorseMessage rawMessage, HorseClient client);

    /// <summary>
    /// Triggered when an exception is thrown while consuming the message
    /// </summary>
    /// <param name="exception">Thrown exception</param>
    /// <param name="model">Message model</param>
    /// <param name="rawMessage">Raw message</param>
    /// <param name="client">Consumer client</param>
    /// <returns></returns>
    Task Error(Exception exception, TModel model, HorseMessage rawMessage, HorseClient client);
}
