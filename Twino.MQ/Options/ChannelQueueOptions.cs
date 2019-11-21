using System;

namespace Twino.MQ.Options
{
    public class ChannelQueueOptions
    {
        /// <summary>
        /// If true, messages will be queued and wait for subscribers to deliver.
        /// If false, only online subsribers receive messages. If there is no subscriber message won't kept.
        /// </summary>
        public bool MessageQueuing { get; set; }

        /// <summary>
        /// If true, messages will send to only first acquirers
        /// </summary>
        public bool SendOnlyFirstAcquirer { get; set; }

        /// <summary>
        /// If true, messages will request delivery from receivers
        /// </summary>
        public bool RequestDelivery { get; set; }

        /// <summary>
        /// When delivery is required, maximum duration for waiting delivery message
        /// </summary>
        public TimeSpan DeliveryWaitMaxDuration { get; set; }

        /// <summary>
        /// When message queuing is active, maximum time for a message wait
        /// </summary>
        public TimeSpan ReceiverWaitMaxDuration { get; set; }

        /// <summary>
        /// If true, server creates unique id for each message.
        /// </summary>
        public bool UseMessageId { get; set; }
    }
}