using System;
using System.Threading.Channels;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Channels
{
    /// <summary>
    /// Messager implementation for Horse Channels
    /// </summary>
    public interface IChannelBus
    {
        /// <summary>
        /// Creates new channel
        /// </summary>
        Task<HorseResult> Create(string channel, Action<ChannelOptions> options = null);

        /// <summary>
        /// Creates new channel
        /// </summary>
        Task<HorseResult> Delete(string channel);

        /// <summary>
        /// Publishes a message to a channel
        /// </summary>
        /// <param name="model">Model</param>
        /// <param name="waitForAcknowledge">If true, methods returns after acknowledge is received from the server</param>
        /// <returns></returns>
        Task<HorseResult> Publish<TModel>(TModel model, bool waitForAcknowledge = false);

        /// <summary>
        /// Publishes a message to a channel
        /// </summary>
        /// <param name="channel">Channel name</param>
        /// <param name="model">Model</param>
        /// <param name="waitForAcknowledge">If true, methods returns after acknowledge is received from the server</param>
        /// <returns></returns>
        Task<HorseResult> Publish<TModel>(string channel, TModel model, bool waitForAcknowledge = false);

        /// <summary>
        /// Publishes a message to a channel
        /// </summary>
        /// <param name="channel">Channel name</param>
        /// <param name="message">String message</param>
        /// <param name="waitForAcknowledge">If true, methods returns after acknowledge is received from the server</param>
        /// <returns></returns>
        Task<HorseResult> Publish(string channel, string message, bool waitForAcknowledge = false);

        /// <summary>
        /// Publishes a message to a channel
        /// </summary>
        /// <param name="channel">Channel name</param>
        /// <param name="data">Binary message</param>
        /// <param name="waitForAcknowledge">If true, methods returns after acknowledge is received from the server</param>
        /// <returns></returns>
        Task<HorseResult> Publish(string channel, byte[] data, bool waitForAcknowledge = false);
    }
}