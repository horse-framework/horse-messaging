using System.IO;

namespace Twino.Protocols.TMQ
{
    public enum MessageType : byte
    {
        Other = 0x00,
        Terminate = 0x08,
        Server = 0x10,
        Channel = 0x11,
        Client = 0x12,
        Delivery = 0x13,
        Response = 0x14,
        Redirect = 0x15,
        Ping = 0x89,
        Pong = 0x8A
    }

    public class TmqMessage
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
                Ttl = 32;

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