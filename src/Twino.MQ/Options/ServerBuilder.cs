using Newtonsoft.Json.Linq;
using System;
using System.Linq;
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
        private IServerMessageHandler _serverMessageHandler;

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

            options.Nodes = _object["Nodes"] != null
                                    ? _object["Nodes"].ToObject<NodeOptions[]>()
                                    : new NodeOptions[0];

            MqServer server = new MqServer(options, _clientAuthenticator, _clientAuthorization);

            if (_channelEventHandler != null)
                server.SetDefaultChannelHandler(_channelEventHandler, _channelAuthenticator);

            if (_messageDeliveryHandler != null)
                server.SetDefaultDeliveryHandler(_messageDeliveryHandler);

            if (_clientHandler != null)
                server.ClientHandler = _clientHandler;

            if (_serverMessageHandler != null)
                server.ServerMessageHandler = _serverMessageHandler;

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

                Channel channel = server.CreateChannel(ctoken.Name, _channelAuthenticator, _channelEventHandler, _messageDeliveryHandler, channelOptions);

                JObject queueToken = ctoken.Value["Queues"] as JObject;
                if (queueToken == null)
                    continue;

                foreach (JProperty qtoken in queueToken.Properties())
                {
                    ushort contentType = Convert.ToUInt16(qtoken.Name);

                    ChannelQueueOptions queueOptions;
                    if (qtoken.HasValues)
                    {
                        queueOptions = CloneFromAsQueue(channelOptions);
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

                    case "rr":
                    case "round":
                    case "robin":
                    case "roundrobin":
                    case "round-robin":
                        options.Status = QueueStatus.RoundRobin;
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
                options.AllowedQueues = allowedContentTypes.Values<ushort>().ToArray();

            JToken allowMultipleQueues = from["AllowMultipleQueues"];
            if (allowMultipleQueues != null)
                options.AllowMultipleQueues = allowMultipleQueues.Value<bool>();

            JToken clientLimit = from["ClientLimit"];
            if (clientLimit != null)
                options.ClientLimit = clientLimit.Value<int>();

            JToken queueLimit = from["QueueLimit"];
            if (queueLimit != null)
                options.QueueLimit = queueLimit.Value<int>();

            JToken destroyWhenEmpty = from["DestroyWhenEmpty"];
            if (destroyWhenEmpty != null)
                options.DestroyWhenEmpty = destroyWhenEmpty.Value<bool>();

            SetQueuePropertyValues(from, options);
        }

        private void SetServerPropertyValues(JObject from, MqServerOptions options)
        {
            SetChannelPropertyValues(from, options);
        }

        private ChannelOptions CloneFrom(ChannelOptions other)
        {
            ChannelOptions options = new ChannelOptions();
            options.AllowedQueues = other.AllowedQueues;
            options.AllowMultipleQueues = other.AllowMultipleQueues;
            options.AcknowledgeTimeout = other.AcknowledgeTimeout;
            options.Status = other.Status;
            options.RequestAcknowledge = other.RequestAcknowledge;
            options.WaitForAcknowledge = other.WaitForAcknowledge;
            options.HideClientNames = other.HideClientNames;
            options.MessageTimeout = other.MessageTimeout;
            options.UseMessageId = other.UseMessageId;
            options.SendOnlyFirstAcquirer = other.SendOnlyFirstAcquirer;
            options.MessageLimit = other.MessageLimit;
            options.MessageSizeLimit = other.MessageSizeLimit;
            options.ClientLimit = other.ClientLimit;
            options.QueueLimit = other.QueueLimit;
            options.DestroyWhenEmpty = other.DestroyWhenEmpty;

            return options;
        }

        private ChannelQueueOptions CloneFromAsQueue(ChannelQueueOptions other)
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
            options.MessageLimit = other.MessageLimit;
            options.MessageSizeLimit = other.MessageSizeLimit;

            return options;
        }

        #endregion

        #region Add

        /// <summary>
        /// Adds client authenticator implementation to builder
        /// </summary>
        public void AddAuthenticator(IClientAuthenticator authenticator)
        {
            _clientAuthenticator = authenticator;
        }

        /// <summary>
        /// Adds client authenticator implementation to builder
        /// </summary>
        public void AddAuthenticator<T>() where T : class, IClientAuthenticator, new()
        {
            _clientAuthenticator = new T();
        }

        /// <summary>
        /// Adds client authenticator implementation to builder
        /// </summary>
        public void AddAuthenticator<T>(params object[] ctorArgs) where T : class, IClientAuthenticator, new()
        {
            IClientAuthenticator authenticator = Activator.CreateInstance(typeof(T), ctorArgs) as IClientAuthenticator;
            _clientAuthenticator = authenticator;
        }

        /// <summary>
        /// Adds authorization implementation to builder
        /// </summary>
        public void AddAuthorization(IClientAuthorization authorization)
        {
            _clientAuthorization = authorization;
        }

        /// <summary>
        /// Adds authorization implementation to builder
        /// </summary>
        public void AddAuthorization<T>() where T : class, IClientAuthorization, new()
        {
            _clientAuthorization = new T();
        }

        /// <summary>
        /// Adds authorization implementation to builder
        /// </summary>
        public void AddAuthorization<T>(params object[] ctorArgs) where T : class, IClientAuthorization, new()
        {
            IClientAuthorization authorization = Activator.CreateInstance(typeof(T), ctorArgs) as IClientAuthorization;
            _clientAuthorization = authorization;
        }

        /// <summary>
        /// Adds client handler implementation to builder
        /// </summary>
        public void AddClientHandler(IClientHandler handler)
        {
            _clientHandler = handler;
        }

        /// <summary>
        /// Adds client handler implementation to builder
        /// </summary>
        public void AddClientHandler<T>() where T : class, IClientHandler, new()
        {
            _clientHandler = new T();
        }

        /// <summary>
        /// Adds client handler implementation to builder
        /// </summary>
        public void AddClientHandler<T>(params object[] ctorArgs) where T : class, IClientHandler, new()
        {
            IClientHandler handler = Activator.CreateInstance(typeof(T), ctorArgs) as IClientHandler;
            _clientHandler = handler;
        }

        /// <summary>
        /// Adds default channel handler implementation to builder
        /// </summary>
        public void AddDefaultChannelHandler(IChannelEventHandler handler)
        {
            _channelEventHandler = handler;
        }

        /// <summary>
        /// Adds default channel handler implementation to builder
        /// </summary>
        public void AddDefaultChannelHandler<T>() where T : class, IChannelEventHandler, new()
        {
            _channelEventHandler = new T();
        }

        /// <summary>
        /// Adds default channel handler implementation to builder
        /// </summary>
        public void AddDefaultChannelHandler<T>(params object[] ctorArgs) where T : class, IChannelEventHandler, new()
        {
            IChannelEventHandler handler = Activator.CreateInstance(typeof(T), ctorArgs) as IChannelEventHandler;
            _channelEventHandler = handler;
        }

        /// <summary>
        /// Adds default channel authenticator implementation to builder
        /// </summary>
        public void AddDefaultChannelAuthenticator(IChannelAuthenticator authenticator)
        {
            _channelAuthenticator = authenticator;
        }

        /// <summary>
        /// Adds default channel authenticator implementation to builder
        /// </summary>
        public void AddDefaultChannelAuthenticator<T>() where T : class, IChannelAuthenticator, new()
        {
            _channelAuthenticator = new T();
        }

        /// <summary>
        /// Adds default channel authenticator implementation to builder
        /// </summary>
        public void AddDefaultChannelAuthenticator<T>(params object[] ctorArgs) where T : class, IChannelAuthenticator, new()
        {
            IChannelAuthenticator authenticator = Activator.CreateInstance(typeof(T), ctorArgs) as IChannelAuthenticator;
            _channelAuthenticator = authenticator;
        }

        /// <summary>
        /// Adds default delivery handler implementation to builder
        /// </summary>
        public void AddDefaultDeliveryHandler(IMessageDeliveryHandler handler)
        {
            _messageDeliveryHandler = handler;
        }

        /// <summary>
        /// Adds default delivery handler implementation to builder
        /// </summary>
        public void AddDefaultDeliveryHandler<T>() where T : class, IMessageDeliveryHandler, new()
        {
            _messageDeliveryHandler = new T();
        }

        /// <summary>
        /// Adds default delivery handler implementation to builder
        /// </summary>
        public void AddDefaultDeliveryHandler<T>(params object[] ctorArgs) where T : class, IMessageDeliveryHandler, new()
        {
            IMessageDeliveryHandler handler = Activator.CreateInstance(typeof(T), ctorArgs) as IMessageDeliveryHandler;
            _messageDeliveryHandler = handler;
        }


        /// <summary>
        /// Adds default delivery handler implementation to builder
        /// </summary>
        public void AddServerMessageHandler(IServerMessageHandler handler)
        {
            _serverMessageHandler = handler;
        }

        /// <summary>
        /// Adds default delivery handler implementation to builder
        /// </summary>
        public void AddServerMessageHandler<T>() where T : class, IServerMessageHandler, new()
        {
            _serverMessageHandler = new T();
        }

        /// <summary>
        /// Adds default delivery handler implementation to builder
        /// </summary>
        public void AddServerMessageHandler<T>(params object[] ctorArgs) where T : class, IServerMessageHandler, new()
        {
            IServerMessageHandler handler = Activator.CreateInstance(typeof(T), ctorArgs) as IServerMessageHandler;
            _serverMessageHandler = handler;
        }

        #endregion
    }
}