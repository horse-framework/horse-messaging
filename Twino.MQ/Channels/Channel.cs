using System;
using System.Collections.Generic;
using Twino.MQ.Clients;
using Twino.MQ.Helpers;
using Twino.MQ.Options;
using Twino.MQ.Security;

namespace Twino.MQ.Channels
{
    public enum ChannelStatus
    {
        None,
        Running,
        Paused,
        Stopped
    }

    public class Channel
    {
        #region Properties

        public string Name { get; }
        public MQServer Server { get; }
        public ChannelOptions Options { get; }

        public ChannelStatus Status { get; private set; }

        public IChannelAuthenticator Authenticator { get; }

        private readonly FlexArray<ushort> _allowedContentTypes = new FlexArray<ushort>(50000);
        public IEnumerable<ushort> AllowedContentTypes => _allowedContentTypes.All();


        public IChannelEventHandler EventHandler { get; }

        private readonly FlexArray<ChannelQueue> _queues;

        public IEnumerable<ChannelQueue> Queues => _queues.All();

        private readonly FlexArray<ChannelClient> _clients;
        public IEnumerable<ChannelClient> Clients => _clients.All();

        #endregion

        #region Constructors

        public Channel(MQServer server,
                       ChannelOptions options,
                       string name,
                       IChannelAuthenticator authenticator,
                       IChannelEventHandler eventHandler)
        {
            Server = server;
            Options = options;
            Name = name;
            Status = ChannelStatus.Running;

            Authenticator = authenticator;
            EventHandler = eventHandler;

            _queues = new FlexArray<ChannelQueue>(options.QueueCapacity);
            _clients = new FlexArray<ChannelClient>(server.Options.ClientCapacity);
        }

        #endregion

        #region Status Actions

        public void SetStatus(ChannelStatus status)
        {
            Status = status;
        }

        #endregion

        #region Queue Actions

        public void CreateQueue()
        {
            throw new NotImplementedException();
        }

        public void RemoveQueue()
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Client Actions

        public void Join(MqClient client)
        {
            throw new NotImplementedException();
        }

        public void Leave(MqClient client)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}