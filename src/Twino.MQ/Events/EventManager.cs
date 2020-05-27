using System.Collections.Generic;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Events
{
    /// <summary>
    /// Manages event subscribers and triggering actions
    /// </summary>
    internal class EventManager
    {
        private readonly List<MqClient> _subscribers = new List<MqClient>();

        /// <summary>
        /// Identifier name of the event
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Event target name (Channel name)
        /// </summary>
        public string Target { get; }

        /// <summary>
        /// Event Content Type (Queue Id)
        /// </summary>
        public ushort ContentType { get; }

        /// <summary>
        /// Name is definition of the event.
        /// Target is the channel name of the event.
        /// Content Type is the Queue Id of the event.
        /// </summary>
        public EventManager(string name, string target, ushort contentType)
        {
            Name = name;
            Target = target;
            ContentType = contentType;
        }

        /// <summary>
        /// Subscribes to an event
        /// </summary>
        public void Subscribe(MqClient client)
        {
            lock (_subscribers)
            {
                if (!_subscribers.Contains(client))
                    _subscribers.Add(client);
            }
        }

        /// <summary>
        /// Unsubscribes from an event
        /// </summary>
        public void Unsubscribe(MqClient client)
        {
            lock (_subscribers)
                _subscribers.Remove(client);
        }

        /// <summary>
        /// Removes all subscriptions
        /// </summary>
        public void ClearSubsscriptions()
        {
            lock (_subscribers)
                _subscribers.Clear();
        }

        /// <summary>
        /// Triggers event and sends message to subscribers
        /// </summary>
        public async Task Trigger(object model)
        {
            if (_subscribers.Count == 0)
                return;

            TmqMessage message = new TmqMessage(MessageType.Event, Target, ContentType);
            message.SetSource(Name);

            if (model != null)
                await message.SetJsonContent(model);

            byte[] data = TmqWriter.Create(message);

            List<MqClient> removing = null;

            lock (_subscribers)
                foreach (MqClient subscriber in _subscribers)
                {
                    //if client is disconnected, add it into removing list
                    if (!subscriber.IsConnected)
                    {
                        //removing list is created when it's needed
                        if (removing == null)
                            removing = new List<MqClient>();

                        removing.Add(subscriber);
                    }
                    else
                        _ = subscriber.SendAsync(data);
                }

            //if there are some removing clients from subscribers list, remove them
            if (removing != null)
                lock (_subscribers)
                    foreach (MqClient remove in removing)
                        _subscribers.Remove(remove);
        }
    }
}