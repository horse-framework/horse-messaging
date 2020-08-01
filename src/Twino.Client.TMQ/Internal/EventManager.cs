using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Internal
{
    internal class EventDescriptor
    {
        public string Channel { get; set; }
        public ushort Queue { get; set; }

        public List<Delegate> Methods { get; set; }

        public Type ParameterType { get; set; }
    }

    internal class EventManager
    {
        private readonly Dictionary<string, List<EventDescriptor>> _descriptors = new Dictionary<string, List<EventDescriptor>>();

        private static readonly string[] EventNames =
        {
            "MessageProduced", "ClientConnected", "ClientDisconnected", "ClientJoined", "ClientLeft", "NodeConnected", "NodeDisconnected",
            "ChannelCreated", "ChannelRemoved", "QueueCreated", "QueueUpdated", "QueueRemoved"
        };

        public EventManager()
        {
            foreach (string eventName in EventNames)
                _descriptors.Add(eventName, new List<EventDescriptor>());
        }

        internal void Add(string eventName, string channel, ushort queue, Delegate method, Type type)
        {
            List<EventDescriptor> list = _descriptors[eventName];
            lock (list)
            {
                EventDescriptor descriptor = list.FirstOrDefault(x => x.Channel == channel && x.Queue == queue);
                if (descriptor == null)
                {
                    descriptor = new EventDescriptor
                                 {
                                     Channel = channel,
                                     Queue = queue,
                                     ParameterType = type,
                                     Methods = new List<Delegate> {method}
                                 };

                    list.Add(descriptor);
                }
                else
                    lock (descriptor.Methods)
                        descriptor.Methods.Add(method);
            }
        }

        internal void Remove(string eventName, string channel, ushort queue)
        {
            List<EventDescriptor> list = _descriptors[eventName];
            lock (list)
            {
                EventDescriptor descriptor = list.FirstOrDefault(x => x.Channel == channel && x.Queue == queue);
                if (descriptor == null)
                    return;

                list.Remove(descriptor);
            }
        }

        internal Task TriggerEvents(TmqClient client, TmqMessage message)
        {
            string eventName = message.Source;
            string channelName = message.Target;
            ushort queueId = message.ContentType;

            List<EventDescriptor> list = _descriptors[eventName];
            EventDescriptor descriptor;

            lock (list)
                descriptor = list.FirstOrDefault(x => x.Channel == channelName && x.Queue == queueId);

            if (descriptor == null)
                return Task.CompletedTask;

            List<Delegate> methods;
            lock (descriptor.Methods)
                methods = new List<Delegate>(descriptor.Methods);

            object param = message.Deserialize(descriptor.ParameterType, client.JsonSerializer);
            foreach (var m in methods)
                m.DynamicInvoke(param);

            return Task.CompletedTask;
        }
    }
}