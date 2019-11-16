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
        /// <param name="data">Data is first 6 bytes of the first received message from the client</param>
        /// <returns></returns>
        Task<bool> Check(byte[] data);
    }

    public interface ITwinoProtocol<TMessage> : ITwinoProtocol
    {
        /// <summary>
        /// Ping message for the specified protocol
        /// </summary>
        byte[] PingMessage { get; }

        /// <summary>
        /// Pong message for the specified protocol
        /// </summary>
        byte[] PongMessage { get; }

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