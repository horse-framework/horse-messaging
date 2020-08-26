using Twino.Protocols.TMQ;

namespace Twino.MQ.Data
{
    /// <summary>
    /// Database file message object
    /// </summary>
    public class DataMessage
    {
        /// <summary>
        /// Message data type
        /// </summary>
        public readonly DataType Type;

        /// <summary>
        /// Message id
        /// </summary>
        public readonly string Id;

        /// <summary>
        /// TMQ Message itself
        /// </summary>
        public readonly TwinoMessage Message;

        /// <summary>
        /// Creates new data message for database IO operations
        /// </summary>
        public DataMessage(DataType type, string id, TwinoMessage message = null)
        {
            Type = type;
            Id = id;
            Message = message;
        }
    }
}