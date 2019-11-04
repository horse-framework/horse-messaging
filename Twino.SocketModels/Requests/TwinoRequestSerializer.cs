using System;
using System.Threading.Tasks;
using Twino.Core;
using Twino.SocketModels.Serialization;

namespace Twino.SocketModels.Requests
{
    internal class TwinoRequestSerializer
    {
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
        internal static async Task<byte[]> SerializeRequest(RequestHeader header, ISocketModel model)
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
            return await WebSocketWriter.CreateFromUTF8Async(message);
        }

        /// <summary>
        /// Creates response websocket message from header and model instances
        /// </summary>
        internal static async Task<byte[]> SerializeResponse(SocketResponse header, ISocketModel model)
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
                await writer.Writer.WriteRawAsync(System.Text.Json.JsonSerializer.Serialize(model, model.GetType()));

            await writer.Writer.WriteEndArrayAsync();

            string message = writer.GetResult();
            return await WebSocketWriter.CreateFromUTF8Async(message);
        }

    }
}