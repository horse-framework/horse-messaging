namespace Twino.MQ.Events
{
    /// <summary>
    /// Name of the Events
    /// </summary>
    internal class EventNames
    {
        /// <summary>
        /// When a client connected to Twino MQ Server.
        /// </summary>
        public const string ClientConnected = "ClientConnected";

        /// <summary>
        /// When a client disconnected from Twino MQ Server.
        /// </summary>
        public const string ClientDisconnected = "ClientDisconnected";

        /// <summary>
        /// When a client joined to a channel.
        /// </summary>
        public const string ClientJoined = "ClientJoined";

        /// <summary>
        /// When a client left a channel.
        /// </summary>
        public const string ClientLeft = "ClientLeft";

        /// <summary>
        /// When a node instance is connected.
        /// </summary>
        public const string NodeConnected = "NodeConnected";

        /// <summary>
        /// When a node instance is disconnected.
        /// </summary>
        public const string NodeDisconnected = "NodeDisconnected";

        /// <summary>
        /// When a channel is created.
        /// </summary>
        public const string ChannelCreated = "ChannelCreated";

        /// <summary>
        /// When options of a channel is changed.
        /// </summary>
        public const string ChannelUpdated = "ChannelUpdated";

        /// <summary>
        /// When a channel and it’s queues are removed.
        /// </summary>
        public const string ChannelRemoved = "ChannelRemoved";

        /// <summary>
        /// When a queue is created in a channel.
        /// </summary>
        public const string QueueCreated = "QueueCreated";

        /// <summary>
        /// When options of a queue is changed.
        /// </summary>
        public const string QueueUpdated = "QueueUpdated";

        /// <summary>
        /// When a queue and it’s messages are removed.
        /// </summary>
        public const string QueueRemoved = "QueueRemoved";

        /// <summary>
        /// When a message is produced into a queue.
        /// </summary>
        public const string MessageProduced = "MessageProduced";
    }
}