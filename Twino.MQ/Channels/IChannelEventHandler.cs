using System.Threading.Tasks;
using Twino.MQ.Clients;

namespace Twino.MQ.Channels
{
    /// <summary>
    /// Channel event handler implementation (client join/leave, queue created/removed, status changes)
    /// </summary>
    public interface IChannelEventHandler
    {
        /// <summary>
        /// Called when a client is joined to a channel
        /// </summary>
        Task OnJoin(ChannelClient client);

        /// <summary>
        /// Called when a client is left from a channel
        /// </summary>
        Task OnLeave(ChannelClient client);

        /// <summary>
        /// Called when a new queue is created in a channel
        /// </summary>
        Task OnQueueCreated(ChannelQueue queue, Channel channel);

        /// <summary>
        /// Called when a queue is removed from a channel
        /// </summary>
        Task OnQueueRemoved(ChannelQueue queue, Channel channel);

        /// <summary>
        /// Called when a channel's status is changed
        /// </summary>
        Task OnStatusChanged(Channel channel, ChannelStatus from, ChannelStatus to);
    }
}