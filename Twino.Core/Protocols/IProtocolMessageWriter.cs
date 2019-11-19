using System.IO;
using System.Threading.Tasks;

namespace Twino.Core.Protocols
{
    /// <summary>
    /// Twino Protocol message writer implementation
    /// </summary>
    public interface IProtocolMessageWriter<in TMessage>
    {
        /// <summary>
        /// Writes message to specified stream.
        /// </summary>
        Task Write(TMessage value, Stream stream);

        /// <summary>
        /// Creates byte array data of the message
        /// </summary>
        Task<byte[]> Create(TMessage value);

        /// <summary>
        /// Creates byte array data of only message header frame
        /// </summary>
        Task<byte[]> CreateFrame(TMessage value);

        /// <summary>
        /// Creates byte array data of only message content
        /// </summary>
        Task<byte[]> CreateContent(TMessage value);
    }
}