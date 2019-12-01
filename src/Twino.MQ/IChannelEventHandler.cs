using System.Threading.Tasks;
using Twino.MQ.Clients;

namespace Twino.MQ
{
    /// <summary>
    /// Channel event handler implementation (client join/leave, queue created/removed, status changes)
    /// </summary>
    public interface IChannelEventHandler
    {
        /// <summary>
        /// Called when a new queue is created in a channel
        /// </summary>
        Task OnQueueCreated(ChannelQueue queue, Channel channel);

        /// <summary>
        /// Called when a queue is removed from a channel
        /// </summary>
        Task OnQueueRemoved(ChannelQueue queue, Channel channel);

        /// <summary>
        /// Called when a client joined to the channel
        /// </summary>
        Task ClientJoined(ChannelClient client);

        /// <summary>
        /// Called when a client left from the channel
        /// </summary>
        Task ClientLeft(ChannelClient client);

        /// <summary>
        /// Called when a channel's status is changed.
        /// If returns false, status change operation will be canceled.
        /// </summary>
        Task<bool> OnChannelStatusChanged(Channel channel, ChannelStatus from, ChannelStatus to);
        
        /// <summary>
        /// Called when queue status has changed
        /// </summary>
        Task<bool> OnQueueStatusChanged(ChannelQueue queue, QueueStatus from, QueueStatus to);
    }
}