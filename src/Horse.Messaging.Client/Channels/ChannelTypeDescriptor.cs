using Horse.Messaging.Client.Internal;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Channels
{
    /// <summary>
    /// Channel type descriptor
    /// </summary>
    public class ChannelTypeDescriptor : ITypeDescriptor
    {
        /// <summary>
        /// Channel name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// If true, name is specified by previous process
        /// </summary>
        public bool ChannelNameSpecified { get; set; }
        
        /// <summary>
        /// Creates new message
        /// </summary>
        public HorseMessage CreateMessage()
        {
            return new HorseMessage(MessageType.Channel, Name, KnownContentTypes.ChannelPush);
        }
    }
}