using System.Threading.Tasks;
using Twino.Core;

namespace Twino.Protocols.TMQ
{
    public class TmqServerSocket : SocketBase
    {
        /// <summary>
        /// WebSocketWriter singleton instance
        /// </summary>
        private static readonly TmqWriter _writer = new TmqWriter();

        /// <summary>
        /// Server of the socket
        /// </summary>
        public ITwinoServer Server { get; }

        /// <summary>
        /// Socket's connection information
        /// </summary>
        public IConnectionInfo Info { get; }

        private readonly IUniqueIdGenerator _uniqueIdGenerator;

        public TmqServerSocket(ITwinoServer server, IConnectionInfo info)
            : this(server, info, new DefaultUniqueIdGenerator())
        {
        }

        public TmqServerSocket(ITwinoServer server, IConnectionInfo info, IUniqueIdGenerator generator)
        {
            Server = server;
            Info = info;
            Stream = info.GetStream();
            _uniqueIdGenerator = generator;
        }

        /// <summary>
        /// Sends TMQ ping message
        /// </summary>
        public override void Ping()
        {
            Send(PredefinedMessages.PING);
        }

        /// <summary>
        /// Sends TMQ pong message
        /// </summary>
        public override void Pong()
        {
            Send(PredefinedMessages.PONG);
        }

        /// <summary>
        /// Sends TMQ message to client
        /// </summary>
        public bool Send(TmqMessage message)
        {
            if (string.IsNullOrEmpty(message.Source))
                message.Source = KnownTargets.HEADER;

            if (string.IsNullOrEmpty(message.MessageId))
                message.MessageId = _uniqueIdGenerator.Create();

            message.CalculateLengths();

            byte[] data = _writer.Create(message).Result;
            return Send(data);
        }

        /// <summary>
        /// Sends TMQ message to client
        /// </summary>
        public async Task<bool> SendAsync(TmqMessage message)
        {
            if (string.IsNullOrEmpty(message.Source))
                message.Source = KnownTargets.HEADER;

            if (string.IsNullOrEmpty(message.MessageId))
                message.MessageId = _uniqueIdGenerator.Create();

            message.CalculateLengths();

            byte[] data = await _writer.Create(message);
            return await SendAsync(data);
        }
    }
}