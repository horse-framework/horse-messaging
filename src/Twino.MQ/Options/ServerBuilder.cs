using System;
using System.Reflection;
using Twino.MQ.Security;
using Twino.Server;

namespace Twino.MQ.Options
{
    public class ServerBuilder
    {
        #region Properties

        private ServerOptions _serverOptions;
        private IClientAuthenticator _clientAuthenticator;
        private IClientAuthorization _clientAuthorization;
        private IClientHandler _clientHandler;
        private IChannelEventHandler _channelEventHandler;
        private IChannelAuthenticator _channelAuthenticator;
        private IMessageDeliveryHandler _messageDeliveryHandler;

        private object _object;

        #endregion

        #region Load

        public void LoadFromFile(string filename)
        {
            LoadFromJson(System.IO.File.ReadAllText(filename));
        }

        public void LoadFromJson(string json)
        {
            _object = Newtonsoft.Json.JsonConvert.DeserializeObject(json);
        }

        #endregion

        #region Create

        public MqServer CreateServer()
        {
            MqServerOptions options = new MqServerOptions();
            SetServerPropertyValues(_object, options);

            MqServer server = new MqServer(_serverOptions, options, _clientAuthenticator, _clientAuthorization);

            if (_channelEventHandler != null)
                server.SetDefaultChannelHandler(_channelEventHandler, _channelAuthenticator);

            if (_messageDeliveryHandler != null)
                server.SetDefaultDeliveryHandler(_messageDeliveryHandler);

            if (_clientHandler != null)
                server.ClientHandler = _clientHandler;

            Type type = _object.GetType();
            PropertyInfo channelList = type.GetProperty("Channels");
            if (channelList == null)
                return server;

            object channels = channelList.GetValue(_object);
            Type channelsType = channels.GetType();
            foreach (PropertyInfo prop in channelsType.GetProperties())
            {
                string channelName = prop.Name;
                object optionsObject = prop.GetValue(channels);
                ChannelOptions channelOptions;
                if (optionsObject != null)
                {
                    channelOptions = new ChannelOptions();
                    SetChannelPropertyValues(optionsObject, channelOptions);
                }
                else
                    channelOptions = options;

                Channel channel = server.CreateChannel(channelName, channelOptions, _channelAuthenticator, _channelEventHandler, _messageDeliveryHandler);
                if (optionsObject == null)
                    continue;

                Type optionsType = optionsObject.GetType();
                PropertyInfo qprop = optionsType.GetProperty("Queues");
                if (qprop == null)
                    continue;

                object queuesObject = qprop.GetValue(optionsObject);
                Type queuesType = queuesObject.GetType();

                foreach (PropertyInfo qp in queuesType.GetProperties())
                {
                    ushort contentType;
                    bool parsed = ushort.TryParse(qp.Name, out contentType);
                    if (!parsed)
                        continue;

                    object queueObject = qp.GetValue(queuesObject);
                    ChannelQueueOptions queueOptions;
                    if (queueObject != null)
                    {
                        queueOptions = new ChannelQueueOptions();
                        SetQueuePropertyValues(queueObject, queueOptions);
                    }
                    else
                        queueOptions = channelOptions;

                    channel.CreateQueue(contentType, queueOptions).Wait();
                }
            }

            return server;
        }

        private void SetQueuePropertyValues(object from, ChannelQueueOptions options)
        {
            throw new NotImplementedException();
        }

        private void SetChannelPropertyValues(object from, ChannelOptions options)
        {
            throw new NotImplementedException();
        }

        private void SetServerPropertyValues(object from, MqServerOptions options)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Add

        public void AddServerOptions(ServerOptions options)
        {
            _serverOptions = options;
        }

        public void AddAuthenticator(IClientAuthenticator authenticator)
        {
            _clientAuthenticator = authenticator;
        }

        public void AddAuthorization(IClientAuthorization authorization)
        {
            _clientAuthorization = authorization;
        }

        public void AddClientHandler(IClientHandler handler)
        {
            _clientHandler = handler;
        }

        public void AddDefaultChannelHandler(IChannelEventHandler handler)
        {
            _channelEventHandler = handler;
        }

        public void AddDefaultChannelAuthenticator(IChannelAuthenticator authenticator)
        {
            _channelAuthenticator = authenticator;
        }

        public void AddDefaultDeliveryHandler(IMessageDeliveryHandler handler)
        {
            _messageDeliveryHandler = handler;
        }

        #endregion
    }
}