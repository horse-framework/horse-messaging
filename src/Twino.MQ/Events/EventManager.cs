using System;
using System.Collections.Generic;
using System.Threading;
using Twino.MQ.Clients;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Events
{
    /// <summary>
    /// Manages event subscribers and triggering actions
    /// </summary>
    public class EventManager : IDisposable
    {
        private readonly List<MqClient> _subscribers = new List<MqClient>();

        /// <summary>
        /// Identifier name of the event
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Event target name (Queue name)
        /// </summary>
        public string Target { get; }

        /// <summary>
        /// Cleanup timer for disconnected subscribers
        /// </summary>
        private Timer _cleanup;

        private readonly TwinoMQ _server;

        /// <summary>
        /// Name is definition of the event.
        /// Target is the queue name of the event.
        /// Content Type is the Queue Id of the event.
        /// </summary>
        protected EventManager(TwinoMQ server, string name, string target)
        {
            _server = server;
            Name = name;
            Target = target;
            _cleanup = new Timer(s => CheckCleanup(), null, 60000, 60000);
        }

        /// <summary>
        /// Checks disconnected clients and removes them from susbcribers list
        /// </summary>
        private void CheckCleanup()
        {
            List<MqClient> removing = new List<MqClient>();
            lock (_subscribers)
            {
                foreach (MqClient s in _subscribers)
                {
                    if (!s.IsConnected)
                        removing.Add(s);
                }

                foreach (MqClient r in removing)
                    _subscribers.Remove(r);
            }
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
            if (_subscribers.Count == 0)
                return;

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
        /// Clears all subscriptions
        /// </summary>
        public void Dispose()
        {
            ClearSubsscriptions();
            _cleanup.Dispose();
            _cleanup = null;
        }

        /// <summary>
        /// Triggers event and sends message to subscribers
        /// </summary>
        protected void Trigger(object model)
        {
            try
            {
                if (_subscribers.Count == 0)
                    return;

                TwinoMessage message = new TwinoMessage(MessageType.Event, Target);
                message.SetSource(Name);

                if (model != null)
                    message.Serialize(model, _server.MessageContentSerializer);

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
            catch (Exception e)
            {
                _server.SendError("EVENT_TRIGGER", e, $"Name:{Name}, Type:{GetType().Name}");
            }
        }
    }
}