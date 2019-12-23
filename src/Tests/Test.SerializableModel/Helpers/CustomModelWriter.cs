using Twino.Protocols.WebSocket;
using Twino.SerializableModel;
using Twino.SerializableModel.Serialization;

namespace Test.SocketModels.Helpers
{
    public class CustomModelWriter : IModelWriter
    {
        private readonly WebSocketWriter _writer = new WebSocketWriter();

        public string Serialize(ISerializableModel model)
        {
            return model.Type + "=" + Newtonsoft.Json.JsonConvert.SerializeObject(model);
        }

        public byte[] Prepare(ISerializableModel model)
        {
            return _writer.Create(Serialize(model)).Result;
        }
    }
}