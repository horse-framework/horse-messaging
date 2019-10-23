using Twino.Core;
using Twino.SocketModels;
using Twino.SocketModels.Serialization;

namespace Test.SocketModels.Helpers
{
    public class CustomModelWriter : IModelWriter
    {
        public string Serialize(ISocketModel model)
        {
            return model.Type + "=" + Newtonsoft.Json.JsonConvert.SerializeObject(model);
        }

        public byte[] Prepare(ISocketModel model)
        {
            return WebSocketWriter.CreateFromUTF8(Serialize(model));
        }
    }
}