using Twino.Core;
using Twino.Server;
using Twino.SocketModels.Serialization;

namespace Twino.SocketModels
{
    public static class Extensions
    {
        
        /// <summary>
        /// Use UserPackageReader instead of this
        /// </summary>
        public static TwinoServer UsePackageManager(this TwinoServer server, PackageReader reader)
        {
            return UsePackageReader(server, reader);
        }

        /// <summary>
        /// Uses package manager for all connections of specified server
        /// </summary>
        public static TwinoServer UsePackageReader(this TwinoServer server, PackageReader reader)
        {
            server.ClientConnected += (s, c) => { c.MessageReceived += reader.Read; };
            server.ClientDisconnected += (s, c) => { c.MessageReceived -= reader.Read; };

            return server;
        }

        /// <summary>
        /// Uses package manager for specified client
        /// </summary>
        public static SocketBase UsePackageReader(this SocketBase client, PackageReader reader)
        {
            client.MessageReceived += reader.Read;
            return client;
        }

        /// <summary>
        /// Creates web socket message from model and sends to specified client.
        /// TwinoModelWriter is used as IModelWriter. Use overload to customize.
        /// </summary>
        public static void Send<TModel>(this SocketBase socket, TModel model) where TModel : ISocketModel
        {
            TwinoModelWriter writer = new TwinoModelWriter();
            socket.Send(WebSocketWriter.CreateFromUTF8(writer.Serialize(model)));
        }
        
        /// <summary>
        /// Creates web socket message from model and sends to specified client
        /// </summary>
        public static void Send<TModel>(this SocketBase socket, TModel model, IModelWriter writer) where TModel : ISocketModel
        {
            byte[] data = WebSocketWriter.CreateFromUTF8(writer.Serialize(model));
            socket.Send(data);
        }

        /// <summary>
        /// Uses default TwinoModelWriter and WebSocketWriter classes.
        /// Creates websocket protocol byte array data of serialized model string. 
        /// </summary>
        public static byte[] ToWebSocketUTF8Data(this ISocketModel model)
        {
            return WebSocketWriter.CreateFromUTF8(TwinoModelWriter.Default.Serialize(model));
        }
        
    }
}