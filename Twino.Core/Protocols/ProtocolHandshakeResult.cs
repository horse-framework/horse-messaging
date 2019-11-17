namespace Twino.Core.Protocols
{
    /// <summary>
    /// Protocol handshaking result
    /// </summary>
    public class ProtocolHandshakeResult
    {
        /// <summary>
        /// If true, server accepted the requested protocol
        /// </summary>
        public bool Accepted { get; set; }

        /// <summary>
        /// When protocol has no protocol recognizing message, protocol must be recognized from first message.
        /// But if we read some part of first message, package reader should be notified.
        /// If this value is true, it means, we read some part of first message. Read part is in PreviouslyRead array.
        /// </summary>
        public bool ReadAfter { get; set; }

        /// <summary>
        /// First 8 bytes of first received data from the connection 
        /// </summary>
        public byte[] PreviouslyRead { get; set; }

        /// <summary>
        /// If protocol handshaking required a message from server to client, this value contains the message
        /// </summary>
        public byte[] Response { get; set; }
    }
}