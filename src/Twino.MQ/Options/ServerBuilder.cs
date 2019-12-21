using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using Twino.MQ.Queues;
using Twino.MQ.Security;

namespace Twino.MQ.Options
{
    /// <summary>
    /// Loads full options from a source and creates new server with these options
    /// </summary>
    public class ServerBuilder
    {
        #region Properties

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

            options.Instances = _object["Instances"] != null
                                    ? _object["Instances"].ToObject<InstanceOptions[]>()
                                    : new InstanceOptions[0];

            MqServer server = new MqServer(options, _clientAuthenticator, _clientAuthorization);

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
            JToken messageQueuing = from["Status"];
            if (messageQueuing != null)
            {
                string status = messageQueuing.Value<string>().ToLower();
                switch (status)
                {
                    case "route":
                        options.Status = QueueStatus.Route;
                        break;
                    case "push":
                        options.Status = QueueStatus.Push;
                        break;
                    case "pull":
                        options.Status = QueueStatus.Pull;
                        break;

                    case "pause":
                    case "paused":
                        options.Status = QueueStatus.Paused;
                        break;

                    case "stop":
                    case "stoped":
                    case "stopped":
                        options.Status = QueueStatus.Stopped;
                        break;
                }
            }

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
                options.MessageTimeout = TimeSpan.FromMilliseconds(messagePendingTimeout.Value<int>());

            JToken useMessageId = from["UseMessageId"];
            if (useMessageId != null)
                options.UseMessageId = useMessageId.Value<bool>();

            JToken waitAcknowledge = from["WaitAcknowledge"];
            if (waitAcknowledge != null)
                options.WaitForAcknowledge = waitAcknowledge.Value<bool>();

            JToken hideClientNames = from["HideClientNames"];
            if (hideClientNames != null)
                options.HideClientNames = hideClientNames.Value<bool>();
        }

        private void SetChannelPropertyValues(JToken from, ChannelOptions options)
        {
            JToken allowedContentTypes = from["AllowedContentTypes"];
            if (allowedContentTypes != null && allowedContentTypes.HasValues)
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
            options.Status = other.Status;
            options.RequestAcknowledge = other.RequestAcknowledge;
            options.WaitForAcknowledge = other.WaitForAcknowledge;
            options.HideClientNames = other.HideClientNames;
            options.MessageTimeout = other.MessageTimeout;
            options.UseMessageId = other.UseMessageId;
            options.SendOnlyFirstAcquirer = other.SendOnlyFirstAcquirer;

            return options;
        }

        private ChannelQueueOptions CloneFrom(ChannelQueueOptions other)
        {
            ChannelQueueOptions options = new ChannelQueueOptions();
            options.AcknowledgeTimeout = other.AcknowledgeTimeout;
            options.Status = other.Status;
            options.RequestAcknowledge = other.RequestAcknowledge;
            options.WaitForAcknowledge = other.WaitForAcknowledge;
            options.HideClientNames = other.HideClientNames;
            options.MessageTimeout = other.MessageTimeout;
            options.UseMessageId = other.UseMessageId;
            options.SendOnlyFirstAcquirer = other.SendOnlyFirstAcquirer;

            return options;
        }

        #endregion

        #region Add

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