using Twino.Protocols.TMQ;

namespace Twino.MQ.Data
{
    public struct DataMessage
    {
        public DataType Type;
        public string Id;
        public TmqMessage Message;

        public DataMessage(DataType type, string id) : this(type, id, null)
        {
        }

        public DataMessage(DataType type, string id, TmqMessage message)
        {
            Type = type;
            Id = id;
            Message = message;
        }
    }
}