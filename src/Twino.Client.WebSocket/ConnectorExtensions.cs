using System.Threading.Tasks;
using Twino.Client.Connectors;
using Twino.Client.WebSocket.Connectors;
using Twino.Protocols.WebSocket;

namespace Twino.Client.WebSocket
{
    public static class ConnectorExtensions
    {
        private static readonly WebSocketWriter _writer = new WebSocketWriter();

        /// <summary>
        /// Sends string websocket message
        /// </summary>
        public static bool Send(this WsStickyConnector connector, string message)
        {
            return SendInternal(connector, message);
        }

        /// <summary>
        /// Sends string websocket message
        /// </summary>
        public static async Task<bool> SendAsync(this WsStickyConnector connector, string message)
        {
            return await SendInternalAsync(connector, message);
        }

        /// <summary>
        /// Sends string websocket message
        /// </summary>
        public static bool Send(this WsAbsoluteConnector connector, string message)
        {
            return SendInternal(connector, message);
        }

        /// <summary>
        /// Sends string websocket message
        /// </summary>
        public static async Task<bool> SendAsync(this WsAbsoluteConnector connector, string message)
        {
            return await SendInternalAsync(connector, message);
        }

        /// <summary>
        /// Sends string websocket message
        /// </summary>
        public static bool Send(this WsNecessityConnector connector, string message)
        {
            return SendInternal(connector, message);
        }

        /// <summary>
        /// Sends string websocket message
        /// </summary>
        public static async Task<bool> SendAsync(this WsNecessityConnector connector, string message)
        {
            return await SendInternalAsync(connector, message);
        }

        /// <summary>
        /// Sends string websocket message
        /// </summary>
        public static bool Send(this WsSingleMessageConnector connector, string message)
        {
            return SendInternal(connector, message);
        }

        /// <summary>
        /// Sends string websocket message
        /// </summary>
        public static async Task<bool> SendAsync(this WsSingleMessageConnector connector, string message)
        {
            return await SendInternalAsync(connector, message);
        }

        /// <summary>
        /// Sends string websocket message
        /// </summary>
        private static bool SendInternal(IConnector<TwinoWebSocket, WebSocketMessage> connector, string message)
        {
            byte[] data = _writer.Create(WebSocketMessage.FromString(message)).Result;
            return connector.Send(data);
        }

        /// <summary>
        /// Sends string websocket message
        /// </summary>
        private static async Task<bool> SendInternalAsync(IConnector<TwinoWebSocket, WebSocketMessage> connector, string message)
        {
            byte[] data = await _writer.Create(WebSocketMessage.FromString(message));
            TwinoWebSocket client = connector.GetClient();

            if (client != null && client.IsConnected)
                return await client.SendAsync(data);

            return false;
        }
    }
}