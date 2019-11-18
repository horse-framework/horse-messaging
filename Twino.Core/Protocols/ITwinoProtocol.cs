using System.Collections.Generic;
using System.Threading.Tasks;

namespace Twino.Core.Protocols
{
    public interface ITwinoProtocol
    {
        /// <summary>
        /// Name of the protocol.
        /// Used for switching protocols. The name should be same value with "Upgrade" HTTP header value.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Checks if data is belong this protocol.
        /// </summary>
        /// <param name="info"></param>
        /// <param name="data">Data is first 8 bytes of the first received message from the client</param>
        /// <returns></returns>
        Task<ProtocolHandshakeResult> Handshake(IConnectionInfo info, byte[] data);

        /// <summary>
        /// When protocol is switched to this protocol from another protocol
        /// </summary>
        Task<ProtocolHandshakeResult> SwitchTo(IConnectionInfo info, Dictionary<string, string> properties);

        /// <summary>
        /// After protocol handshake is completed, this method is called to handle events for the specified client
        /// </summary>
        Task HandleConnection(IConnectionInfo info, ProtocolHandshakeResult handshakeResult);
    }

    public interface ITwinoProtocol<TMessage> : ITwinoProtocol
    {
        /// <summary>
        /// Client and message handler for the protocol
        /// </summary>
        IProtocolConnectionHandler<TMessage> Handler { get; }

        /// <summary>
        /// Creates a protocol reader for the specific client
        /// </summary>
        /// <returns></returns>
        IProtocolMessageReader<TMessage> CreateReader();

        /// <summary>
        /// Creates a protocol writer for the specific client
        /// </summary>
        /// <returns></returns>
        IProtocolMessageWriter<TMessage> CreateWriter();
    }
}