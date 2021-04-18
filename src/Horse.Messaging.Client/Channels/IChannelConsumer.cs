using System;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Channels
{
    /// <summary>
    /// Consumer for horse channel
    /// </summary>
    public interface IChannelConsumer<in TModel>
    {
        /// <summary>
        /// Triggered when a message is received from a channel
        /// </summary>
        /// <param name="model">Message model</param>
        /// <param name="rawMessage">Raw message</param>
        /// <param name="client">Consumer client</param>
        /// <returns></returns>
        Task Consume(TModel model, HorseMessage rawMessage, HorseClient client);

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

    /// <summary>
    /// Consumer for horse channel
    /// </summary>
    public interface IChannelConsumer
    {
        /// <summary>
        /// Triggered when a message is received from a channel
        /// </summary>
        /// <param name="rawMessage">Raw message</param>
        /// <param name="client">Consumer client</param>
        /// <returns></returns>
        Task Consume(HorseMessage rawMessage, HorseClient client);

        /// <summary>
        /// Triggered when an exception is thrown while consuming the message
        /// </summary>
        /// <param name="exception">Thrown exception</param>
        /// <param name="rawMessage">Raw message</param>
        /// <param name="client">Consumer client</param>
        /// <returns></returns>
        Task Error(Exception exception, HorseMessage rawMessage, HorseClient client);
    }
}