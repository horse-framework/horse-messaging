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
        /// "481" Duplicate record, such as, you might send create queue operation when client is already created
        /// </summary>
        public const ushort Duplicate = 481;

        /// <summary>
        /// "482" Limit exceeded, such as, maximum queue limit of the server
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
        /// "601" Subscribe to a queue
        /// </summary>
        public const ushort Subscribe = 601;

        /// <summary>
        /// "602" Unsubscribe from a queue
        /// </summary>
        public const ushort Unsubscribe = 602;

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
    }
}