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
        /// If true, initial channel message supported by the channel
        /// </summary>
        public bool InitialChannelMessage { get; set; }

        /// <summary>
        /// Creates new message
        /// </summary>
        public HorseMessage CreateMessage(string overwrittenTarget = null)
        {
            HorseMessage message = new HorseMessage(MessageType.Channel, overwrittenTarget ?? Name, KnownContentTypes.ChannelPush);

            if (InitialChannelMessage)
                message.AddHeader(HorseHeaders.CHANNEL_INITIAL_MESSAGE, "1");

            return message;
        }
    }
}