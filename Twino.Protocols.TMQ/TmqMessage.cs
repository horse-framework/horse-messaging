using System.IO;
using System.Text;

namespace Twino.Protocols.TMQ
{
    /// <summary>
    /// TMQ Protocol message
    /// </summary>
    public class TmqMessage
    {
        /// <summary>
        /// If true, receiver is the first acquirer of the message
        /// </summary>
        public bool FirstAcquirer { get; set; }

        /// <summary>
        /// If true, message should be at first element in the queue
        /// </summary>
        public bool HighPriority { get; set; }

        /// <summary>
        /// Message type
        /// </summary>
        public MessageType Type { get; set; }

        /// <summary>
        /// True means, client is pending a response. Sending response is not mandatory but it SHOULD sent.
        /// </summary>
        public bool ResponseRequired { get; set; }

        /// <summary>
        /// Message TTL value. Default is 16
        /// </summary>
        public int Ttl { get; set; } = 16;

        /// <summary>
        /// Message Id length
        /// </summary>
        public int MessageIdLength { get; set; }

        /// <summary>
        /// Message unique id
        /// </summary>
        public string MessageId { get; set; }

        /// <summary>
        /// Message target id length
        /// </summary>
        public int TargetLength { get; set; }

        /// <summary>
        /// Message target (channel name, client name or server)
        /// </summary>
        public string Target { get; set; }

        /// <summary>
        /// Message source length
        /// </summary>
        public int SourceLength { get; set; }

        /// <summary>
        /// Message source client unique id, channel unique id or server
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Message content length
        /// </summary>
        public ulong Length { get; set; }

        /// <summary>
        /// Message content stream
        /// </summary>
        public MemoryStream Content { get; set; }

        /// <summary>
        /// Checks message id, source, target and content properties.
        /// If they have a value, sets to length properties to their lengths
        /// </summary>
        public void CalculateLengths()
        {
            if (MessageId != null)
                MessageIdLength = MessageId.Length;

            if (Source != null)
                SourceLength = Source.Length;

            if (Target != null)
                TargetLength = Target.Length;

            if (Content != null)
                Length = (ulong) Content.Length;
        }

        public override string ToString()
        {
            if (Content == null)
                return string.Empty;

            return Encoding.UTF8.GetString(Content.ToArray());
        }
    }
}