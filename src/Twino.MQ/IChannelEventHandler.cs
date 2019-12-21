using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Queues;

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
        Task OnClientJoined(ChannelClient client);

        /// <summary>
        /// Called when a client left from the channel
        /// </summary>
        Task OnClientLeft(ChannelClient client);

        /// <summary>
        /// Called when queue status has changed
        /// </summary>
        Task<bool> OnQueueStatusChanged(ChannelQueue queue, QueueStatus from, QueueStatus to);
        
        /// <summary>
        /// Called when channel is removed
        /// </summary>
        Task OnChannelRemoved(Channel channel);

    }
}