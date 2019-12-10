using System;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Newtonsoft.Json.Linq;
using Twino.MQ.Security;
using Twino.Server;

namespace Twino.MQ.Options
{
    /// <summary>
    /// Loads full options from a source and creates new server with these options
    /// </summary>
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

        private JObject _object;

        #endregion

        #region Load

        /// <summary>
        /// Loads JSON from file
        /// </summary>
        public void LoadFromFile(string filename)
        {
            LoadFromJson(System.IO.File.ReadAllText(filename));
        }

        /// <summary>
        /// Loads options from JSON string
        /// </summary>
        public void LoadFromJson(string json)
        {
            _object = JObject.Parse(json);
        }

        #endregion

        #region Create

        /// <summary>
        /// Creates server from loaded and added options
        /// </summary>
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

            JObject channelToken = _object["Channels"] as JObject;
            if (channelToken == null)
                return server;

            foreach (JProperty ctoken in channelToken.Properties())
            {
                ChannelOptions channelOptions;
                if (ctoken.HasValues)
                {
                    channelOptions = CloneFrom(options);
                    SetChannelPropertyValues(ctoken.Value, channelOptions);
                }
                else
                    channelOptions = options;

                Channel channel = server.CreateChannel(ctoken.Name, channelOptions, _channelAuthenticator, _channelEventHandler, _messageDeliveryHandler);

                JObject queueToken = ctoken.Value["Queues"] as JObject;
                if (queueToken == null)
                    continue;

                foreach (JProperty qtoken in queueToken.Properties())
                {
                    ushort contentType = Convert.ToUInt16(qtoken.Name);

                    ChannelQueueOptions queueOptions;
                    if (qtoken.HasValues)
                    {
                        queueOptions = CloneFrom(channelOptions);
                        SetQueuePropertyValues(qtoken.Value, queueOptions);
                    }
                    else
                        queueOptions = channelOptions;

                    channel.CreateQueue(contentType, queueOptions).Wait();
                }
            }

            return server;
        }

        private void SetQueuePropertyValues(JToken from, ChannelQueueOptions options)
        {
            JToken messageQueuing = from["MessageQueuing"];
            if (messageQueuing != null)
                options.MessageQueuing = messageQueuing.Value<bool>();

            JToken sendOnlyFirstAcquirer = from["SendOnlyFirstAcquirer"];
            if (sendOnlyFirstAcquirer != null)
                options.SendOnlyFirstAcquirer = sendOnlyFirstAcquirer.Value<bool>();

            JToken requestAcknowledge = from["RequestAcknowledge"];
            if (requestAcknowledge != null)
                options.RequestAcknowledge = requestAcknowledge.Value<bool>();

            JToken acknowledgeTimeout = from["AcknowledgeTimeout"];
            if (acknowledgeTimeout != null)
                options.AcknowledgeTimeout = TimeSpan.FromMilliseconds(acknowledgeTimeout.Value<int>());

            JToken messagePendingTimeout = from["MessagePendingTimeout"];
            if (messagePendingTimeout != null)
                options.MessagePendingTimeout = TimeSpan.FromMilliseconds(messagePendingTimeout.Value<int>());

            JToken useMessageId = from["UseMessageId"];
            if (useMessageId != null)
                options.UseMessageId = useMessageId.Value<bool>();

            JToken waitAcknowledge = from["WaitAcknowledge"];
            if (waitAcknowledge != null)
                options.WaitAcknowledge = waitAcknowledge.Value<bool>();

            JToken hideClientNames = from["HideClientNames"];
            if (hideClientNames != null)
                options.HideClientNames = hideClientNames.Value<bool>();
        }

        private void SetChannelPropertyValues(JToken from, ChannelOptions options)
        {
            JToken allowedContentTypes = from["AllowedContentTypes"];
            if (allowedContentTypes != null)
                options.AllowedContentTypes = allowedContentTypes.Values<ushort>().ToArray();

            JToken allowMultipleQueues = from["AllowMultipleQueues"];
            if (allowMultipleQueues != null)
                options.AllowMultipleQueues = allowMultipleQueues.Value<bool>();

            SetQueuePropertyValues(from, options);
        }

        private void SetServerPropertyValues(JObject from, MqServerOptions options)
        {
            SetChannelPropertyValues(from, options);
        }

        private ChannelOptions CloneFrom(ChannelOptions other)
        {
            ChannelOptions options = new ChannelOptions();
            options.AllowedContentTypes = other.AllowedContentTypes;
            options.AllowMultipleQueues = other.AllowMultipleQueues;
            options.AcknowledgeTimeout = other.AcknowledgeTimeout;
            options.MessageQueuing = other.MessageQueuing;
            options.RequestAcknowledge = other.RequestAcknowledge;
            options.WaitAcknowledge = other.WaitAcknowledge;
            options.HideClientNames = other.HideClientNames;
            options.MessagePendingTimeout = other.MessagePendingTimeout;
            options.UseMessageId = other.UseMessageId;
            options.SendOnlyFirstAcquirer = other.SendOnlyFirstAcquirer;

            return options;
        }
        
        private ChannelQueueOptions CloneFrom(ChannelQueueOptions other)
        {
            ChannelQueueOptions options = new ChannelQueueOptions();
            options.AcknowledgeTimeout = other.AcknowledgeTimeout;
            options.MessageQueuing = other.MessageQueuing;
            options.RequestAcknowledge = other.RequestAcknowledge;
            options.WaitAcknowledge = other.WaitAcknowledge;
            options.HideClientNames = other.HideClientNames;
            options.MessagePendingTimeout = other.MessagePendingTimeout;
            options.UseMessageId = other.UseMessageId;
            options.SendOnlyFirstAcquirer = other.SendOnlyFirstAcquirer;
            
            return options;
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