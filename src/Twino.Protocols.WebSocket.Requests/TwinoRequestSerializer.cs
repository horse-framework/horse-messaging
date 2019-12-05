using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Twino.JsonModel;
using Twino.JsonModel.Serialization;

namespace Twino.Protocols.WebSocket.Requests
{
    /// <summary>
    /// Serializes and deserializes WebSocket requests and responses
    /// </summary>
    internal class TwinoRequestSerializer
    {
        private static readonly WebSocketWriter _writer = new WebSocketWriter();

        /// <summary>
        /// Definition of serialized package as request if it starts with "REQ="
        /// </summary>
        internal const string REQUEST_CODE = "REQ";

        /// <summary>
        /// Definition of serialized package as response if it starts with "RES="
        /// </summary>
        internal const string RESPONSE_CODE = "RES";

        /// <summary>
        /// Read Request or Response header model from JSON string
        /// </summary>
        internal static T DeserializeHeader<T>(string kind, string message) where T : class, new()
        {
            if (!message.StartsWith(kind + "="))
                return null;

            int headerStart = message.IndexOf('{');
            int headerEnd = message.IndexOf('}');

            if (headerStart < 0 || headerEnd < 0 || headerEnd <= headerStart)
                return null;

            string serialized = message.Substring(headerStart, headerEnd - headerStart + 1);
            T header = System.Text.Json.JsonSerializer.Deserialize<T>(serialized);
            return header;
        }

        /// <summary>
        /// Read Request or Response model from JSON string
        /// </summary>
        internal static object DeserializeModel(Type type, string kind, string message)
        {
            if (!message.StartsWith(kind + "="))
                return null;

            int headerStart = message.IndexOf('{');
            int headerEnd = message.IndexOf('}');

            if (headerStart < 0 || headerEnd < 0 || headerEnd <= headerStart)
                return null;

            int modelStart = message.IndexOf('{', headerEnd);
            int modelEnd = message.LastIndexOf('}');

            string serialized = message.Substring(modelStart, modelEnd - modelStart + 1);
            object model;
            bool critical = typeof(IPerformanceCriticalModel).IsAssignableFrom(type);
            if (critical)
            {
                IPerformanceCriticalModel criticalModel = (IPerformanceCriticalModel) Activator.CreateInstance(type);

                LightJsonReader reader = new LightJsonReader(serialized);
                reader.StartObject();
                criticalModel.Deserialize(reader);
                reader.EndObject();

                model = criticalModel;
            }
            else
                model = System.Text.Json.JsonSerializer.Deserialize(serialized, type);

            return model;
        }

        /// <summary>
        /// Creates request websocket message from header and model instances
        /// </summary>
        internal static async Task<byte[]> SerializeRequest(RequestHeader header, ISerializableModel model)
        {
            LightJsonWriter writer = new LightJsonWriter();

            await writer.Writer.WriteRawAsync(REQUEST_CODE + "=");
            await writer.Writer.WriteStartArrayAsync();

            //header
            writer.StartObject();
            writer.Write("unique", header.Unique);
            writer.Write("requestType", header.RequestType);
            writer.Write("responseType", header.ResponseType);
            writer.EndObject();
            await writer.Writer.WriteRawAsync(",");

            if (model is IPerformanceCriticalModel critical)
            {
                writer.StartObject();
                critical.Serialize(writer);
                writer.EndObject();
            }
            else
                await writer.Writer.WriteRawAsync(System.Text.Json.JsonSerializer.Serialize(model, model.GetType()));

            await writer.Writer.WriteEndArrayAsync();

            string message = writer.GetResult();
            WebSocketMessage wsmsg = new WebSocketMessage
                                     {
                                         OpCode = SocketOpCode.UTF8,
                                         Content = new MemoryStream(Encoding.UTF8.GetBytes(message))
                                     };

            return await _writer.Create(wsmsg);
        }

        /// <summary>
        /// Creates response websocket message from header and model instances
        /// </summary>
        internal static async Task<byte[]> SerializeResponse(SocketResponse header, ISerializableModel model)
        {
            LightJsonWriter writer = new LightJsonWriter();
            await writer.Writer.WriteRawAsync(RESPONSE_CODE + "=");
            await writer.Writer.WriteStartArrayAsync();

            //header
            writer.StartObject();
            writer.Write("unique", header.Unique);
            writer.Write("requestType", header.RequestType);
            writer.Write("responseType", header.ResponseType);
            writer.Write("status", header.Status);
            writer.EndObject();
            await writer.Writer.WriteRawAsync(",");

            if (model is IPerformanceCriticalModel critical)
            {
                writer.StartObject();
                critical.Serialize(writer);
                writer.EndObject();
            }
            else
            {
                if (model == null)
                    await writer.Writer.WriteRawAsync("null");
                else
                    await writer.Writer.WriteRawAsync(System.Text.Json.JsonSerializer.Serialize(model, model.GetType()));
            }

            await writer.Writer.WriteEndArrayAsync();

            string message = writer.GetResult();
            WebSocketMessage wsmsg = new WebSocketMessage
                                     {
                                         OpCode = SocketOpCode.UTF8,
                                         Content = new MemoryStream(Encoding.UTF8.GetBytes(message))
                                     };

            return await _writer.Create(wsmsg);
        }
    }
}