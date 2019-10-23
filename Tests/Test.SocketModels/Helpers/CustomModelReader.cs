using System;
using Twino.SocketModels;
using Twino.SocketModels.Serialization;

namespace Test.SocketModels.Helpers
{
    public class CustomModelReader : IModelReader
    {
        public T Read<T>(string serialized) where T : ISocketModel, new()
        {
            return Read<T>(serialized, true);
        }

        public T Read<T>(string serialized, bool verify) where T : ISocketModel, new()
        {
            int index;
            int type = ReadType(serialized, out index);

            T model = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(serialized.Substring(index + 1));

            if (verify && type != model.Type)
                return default;

            return model;
        }

        public ISocketModel Read(Type type, string serialized)
        {
            return Read(type, serialized, true);
        }

        public ISocketModel Read(Type type, string serialized, bool verify)
        {
            int index;
            int code = ReadType(serialized, out index);

            ISocketModel model = (ISocketModel) Newtonsoft.Json.JsonConvert.DeserializeObject(serialized.Substring(index + 1), type);

            if (verify && code != model.Type)
                return null;

            return model;
        }

        public int ReadType(string serialized)
        {
            return ReadType(serialized, out _);
        }

        public int ReadType(string serialized, out int index)
        {
            index = serialized.IndexOf('=');
            string type = serialized.Substring(0, index);

            return Convert.ToInt32(type);
        }
    }
}