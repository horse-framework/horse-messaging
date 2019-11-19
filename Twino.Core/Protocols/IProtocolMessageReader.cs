using System.IO;
using System.Threading.Tasks;

namespace Twino.Core.Protocols
{
    /// <summary>
    /// Twino Protocol message reader implementation
    /// </summary>
    public interface IProtocolMessageReader<TMessage>
    {
        /// <summary>
        /// Protocol handshaking information.
        /// Many protocol reading operations require to know how handshaking completed 
        /// </summary>
        ProtocolHandshakeResult HandshakeResult { get; set; }

        /// <summary>
        /// Reads message from stream
        /// </summary>
        Task<TMessage> Read(Stream stream);

        /// <summary>
        /// Resets read operation sources and prepares the reader to read again
        /// </summary>
        void Reset();
    }
}