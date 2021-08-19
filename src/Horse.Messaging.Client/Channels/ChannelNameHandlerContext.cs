using System;

namespace Horse.Messaging.Client.Channels
{
    /// <summary>
    /// Channel name handler context
    /// </summary>
    public class ChannelNameHandlerContext
    {
        /// <summary>
        /// Client
        /// </summary>
        public HorseClient Client { get; set; }

        /// <summary>
        /// Model type
        /// </summary>
        public Type Type { get; set; }
    }
}