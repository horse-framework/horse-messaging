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
        /// Called when a new queue is created in a channel
        /// </summary>
        Task OnQueueCreated(ChannelQueue queue, Channel channel);

        /// <summary>
        /// Called when a queue is removed from a channel
        /// </summary>
        Task OnQueueRemoved(ChannelQueue queue, Channel channel);

        /// <summary>
        /// Called when a channel's status is changed.
        /// If returns false, status change operation will be canceled.
        /// </summary>
        Task<bool> OnStatusChanged(Channel channel, ChannelStatus from, ChannelStatus to);
    }
}