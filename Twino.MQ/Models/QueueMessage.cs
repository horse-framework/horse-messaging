using System.IO;

namespace Twino.MQ.Models
{
    public enum MessageType : byte
    {
        Other = 0x00,
        Hello = 0x01,
        Terminate = 0x08,
        Ping = 0x09,
        Pong = 0x0A,
        Server = 0x10,
        Channel = 0x11,
        Client = 0x12,
        Delivery = 0x13,
        Response = 0x14,
        Redirect = 0x15
    }

    /* TYPE OCTET
     * type byte: [abcxxxxx] -> a = 1:first acquirer 0:not first acquirer
     *                          b = 1:high priority 0:default
     *                          x = 5 bits message type
     */

    /* TTL OCTET
     * [axxxxxxx] -> a = 1:sender waiting response
     *               x = 7 bits TTL
     */

    /* MESSAGE LENGTH
     * x: first byte;
     *    x < 253     => x = length
     *    x = 253     => next 2 bytes ushort length
     *    x = 254     => next 4 bytes uint length
     *    x = 255     => next 8 bytes ulong length
     */

    /* FRAME
     * ========
     * [type:octet] [ttl:octet]
     * [id-length:octet] [source-length:octet] [target-length:octet] [message-length:flex]
     * [id-payload:ilen] [source-data:slen] [target-data:tlen] [payload:len]
     *
     * NOTE: Min 6 bytes required: (type,ttl,ilen,slen,tlen,len)
     */

    public class QueueMessage
    {
        public bool FirstAcquirer { get; set; }
        public bool HighPriority { get; set; }
        public MessageType Type { get; set; }
        public bool ResponseRequired { get; set; }
        public int Ttl { get; set; }

        public int MessageIdLength { get; set; }
        public string MessageId { get; set; }

        public int TargetLength { get; set; }
        public string Target { get; set; }

        public int SourceLength { get; set; }
        public string Source { get; set; }

        public ulong Length { get; set; }

        public MemoryStream Content { get; set; }

        public void PrepareFirstUse()
        {
            if (Ttl == 0)
                Ttl = 64;

            if (MessageId != null)
                MessageIdLength = MessageId.Length;

            if (Source != null)
                SourceLength = Source.Length;

            if (Target != null)
                TargetLength = Target.Length;

            if (Content != null)
                Length = (ulong) Content.Length;
        }
    }
}