using System.Security.Cryptography.X509Certificates;

namespace Horse.Messaging.Protocol
{
    /// <summary>
    /// Known content types for MQ Server with Horse protocol
    /// </summary>
    public class KnownContentTypes
    {
        /// <summary>
        /// "500" Process failed
        /// </summary>
        public const ushort Failed = 1;

        /// <summary>
        /// "101" After procotol handshake completed, first message is the hello message 
        /// </summary>
        public const ushort Hello = 101;

        /// <summary>
        /// "202" Message is accepted
        /// </summary>
        public const ushort Accepted = 202;

        /// <summary>
        /// "204" No content
        /// </summary>
        public const ushort NoContent = 204;

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
        /// "481" Duplicate record, such as, you might send create queue operation when client is already created
        /// </summary>
        public const ushort Duplicate = 481;

        /// <summary>
        /// "482" Limit exceeded, such as, maximum queue limit of the server
        /// </summary>
        public const ushort LimitExceeded = 482;

        /// <summary>
        /// "503" Server is too busy to handle the message
        /// </summary>
        public const ushort Busy = 503;

        /// <summary>
        /// "601" Subscribe to a queue
        /// </summary>
        public const ushort QueueSubscribe = 601;

        /// <summary>
        /// "602" Unsubscribe from a queue
        /// </summary>
        public const ushort QueueUnsubscribe = 602;

        /// <summary>
        /// "607" Gets all consumers of a queue
        /// </summary>
        public const ushort QueueConsumers = 607;

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
        /// "616" Gets queue information list
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

        /// <summary>
        /// "651" Gets all rouuters
        /// </summary>
        public const ushort ListRouters = 651;

        /// <summary>
        /// "652" Creates new router
        /// </summary>
        public const ushort CreateRouter = 652;

        /// <summary>
        /// "653" Removes a router with it's bindings
        /// </summary>
        public const ushort RemoveRouter = 653;

        /// <summary>
        /// "661" List all bindings of a router
        /// </summary>
        public const ushort ListBindings = 661;

        /// <summary>
        /// "662" Creates new binding in a router
        /// </summary>
        public const ushort AddBinding = 662;

        /// <summary>
        /// "663" Removes a binding from a router
        /// </summary>
        public const ushort RemoveBinding = 663;

        /// <summary>
        /// "671" Gets a cache value
        /// </summary>
        public const ushort GetCache = 671;

        /// <summary>
        /// "672" Adds or sets a cache
        /// </summary>
        public const ushort SetCache = 672;

        /// <summary>
        /// "673" Removes a cache
        /// </summary>
        public const ushort RemoveCache = 673;

        /// <summary>
        /// "674" Purges all cache keys 
        /// </summary>
        public const ushort PurgeCache = 674;

        /// <summary>
        /// "681" Pushes a message to channel
        /// </summary>
        public const ushort ChannelPush = 681;

        /// <summary>
        /// "682" Creates new channel
        /// </summary>
        public const ushort ChannelCreate = 682;

        /// <summary>
        /// "683" Updates a channel options
        /// </summary>
        public const ushort ChannelUpdate = 683;

        /// <summary>
        /// "684" Removes a channel
        /// </summary>
        public const ushort ChannelRemove = 684;

        /// <summary>
        /// "685" Gets channel list
        /// </summary>
        public const ushort ChannelList = 685;

        /// <summary>
        /// "686" Subscribes to a channel
        /// </summary>
        public const ushort ChannelSubscribe = 686;

        /// <summary>
        /// "686" Unsubscribes from a channel
        /// </summary>
        public const ushort ChannelUnsubscribe = 687;
        
        /// <summary>
        /// "691" Create and begin new transaction
        /// </summary>
        public const ushort TransactionBegin = 691;
        
        /// <summary>
        /// "692" Commit a transaction
        /// </summary>
        public const ushort TransactionCommit = 692;
        
        /// <summary>
        /// "693" rollback a transaction
        /// </summary>
        public const ushort TransactionRollback = 693;
    }
}