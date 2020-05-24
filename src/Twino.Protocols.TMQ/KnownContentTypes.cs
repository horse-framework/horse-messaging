namespace Twino.Protocols.TMQ
{
    /// <summary>
    /// Known content types for MQ Server with TMQ protocol
    /// </summary>
    public class KnownContentTypes
    {
        /// <summary>
        /// "101" After procotol handshake completed, first message is the hello message 
        /// </summary>
        public const ushort Hello = 101;

        /// <summary>
        /// "200" Operation successful
        /// </summary>
        public const ushort Ok = 200;

        /// <summary>
        /// "202" Message is accepted
        /// </summary>
        public const ushort Accepted = 202;

        /// <summary>
        /// "400" Message has invalid content
        /// </summary>
        public const ushort BadRequest = 400;

        /// <summary>
        /// "401" Permission denied for client
        /// </summary>
        public const ushort Unauthorized = 401;

        /// <summary>
        /// "404" Requested data not found
        /// </summary>
        public const ushort NotFound = 404;

        /// <summary>
        /// "406" Message is unacceptable
        /// </summary>
        public const ushort Unacceptable = 406;

        /// <summary>
        /// "481" Duplicate record, such as, you might send create channel operation when client is already created
        /// </summary>
        public const ushort Duplicate = 481;

        /// <summary>
        /// "482" Limit exceeded, such as, maximum queue limit of a channel
        /// </summary>
        public const ushort LimitExceeded = 482;

        /// <summary>
        /// "500" Process failed
        /// </summary>
        public const ushort Failed = 500;

        /// <summary>
        /// "503" Server is too busy to handle the message
        /// </summary>
        public const ushort Busy = 503;

        /// <summary>
        /// "601" Join to channel
        /// </summary>
        public const ushort Join = 601;

        /// <summary>
        /// "602" Leave from channel
        /// </summary>
        public const ushort Leave = 602;

        /// <summary>
        /// "603" Create new channel
        /// </summary>
        public const ushort CreateChannel = 603;

        /// <summary>
        /// "604" Delete channel with it's queues
        /// </summary>
        public const ushort RemoveChannel = 604;

        /// <summary>
        /// "605" Gets channel information
        /// </summary>
        public const ushort ChannelInformation = 605;

        /// <summary>
        /// "606" Gets active channel list in server
        /// </summary>
        public const ushort ChannelList = 606;

        /// <summary>
        /// "607" Gets all consumers of a channel
        /// </summary>
        public const ushort ChannelConsumers = 607;

        /// <summary>
        /// "610" Creates new queue
        /// </summary>
        public const ushort CreateQueue = 610;

        /// <summary>
        /// "611" Deletes the queue with it's messages
        /// </summary>
        public const ushort RemoveQueue = 611;

        /// <summary>
        /// "612" Changes queue properties and/or status
        /// </summary>
        public const ushort UpdateQueue = 612;

        /// <summary>
        /// "613" Clears messages in queue
        /// </summary>
        public const ushort ClearMessages = 613;

        /// <summary>
        /// "615" Gets queue information
        /// </summary>
        public const ushort QueueInformation = 615;

        /// <summary>
        /// "616" Gets queue information list of a channel
        /// </summary>
        public const ushort QueueList = 616;

        /// <summary>
        /// "621" Gets active instance list
        /// </summary>
        public const ushort InstanceList = 621;

        /// <summary>
        /// "631" Gets all connected clients
        /// </summary>
        public const ushort ClientList = 631;

        /// <summary>
        /// "641" Node instance sends a decision to other nodes
        /// </summary>
        public const ushort DecisionOverNode = 641;

    }
}