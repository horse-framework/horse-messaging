namespace Twino.Protocols.TMQ
{
    /// <summary>
    /// TMQ Message types
    /// </summary>
    public enum MessageType : byte
    {
        /// <summary>
        /// Unknown message, may be peer to peer
        /// </summary>
        Other = 0x00,

        /// <summary>
        /// Connection close request
        /// </summary>
        Terminate = 0x08,

        /// <summary>
        /// Ping message from server
        /// </summary>
        Ping = 0x09,

        /// <summary>
        /// Pong message to server
        /// </summary>
        Pong = 0x0A,

        /// <summary>
        /// A message to directly server.
        /// Server should deal with it directly.
        /// </summary>
        Server = 0x10,

        /// <summary>
        /// A message to a channel
        /// </summary>
        Channel = 0x11,

        /// <summary>
        /// A message to an other client
        /// </summary>
        Client = 0x12,

        /// <summary>
        /// A acknowledge message, points to a message received before.
        /// </summary>
        Acknowledge = 0x13,

        /// <summary>
        /// A response message, point to a message received before.
        /// </summary>
        Response = 0x14,

        /// <summary>
        /// A redirection message, message is same but has a new target 
        /// </summary>
        Redirect = 0x15
    }
}