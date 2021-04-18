using System;
using Horse.Messaging.Client;
using Horse.Mq.Client;

namespace Horse.Messaging.Bus
{
    internal class ConnectorInstanceCreator
    {
        private readonly string _id;
        private readonly string _name;
        private readonly string _type;
        private readonly string _token;
        private readonly Action<HorseClient> _enhance;

        public ConnectorInstanceCreator(string id, string name, string type, string token, Action<HorseClient> enchangeAction)
        {
            _id = id;
            _name = name;
            _type = type;
            _token = token;
            _enhance = enchangeAction;
        }

        public HorseClient CreateInstance()
        {
            HorseClient client = new HorseClient();

            if (!string.IsNullOrEmpty(_id))
                client.ClientId = _id;

            if (!string.IsNullOrEmpty(_name))
                client.SetClientName(_name);

            if (!string.IsNullOrEmpty(_type))
                client.SetClientType(_type);

            if (!string.IsNullOrEmpty(_token))
                client.SetClientToken(_token);

            if (_enhance != null)
                _enhance(client);

            return client;
        }
    }
}