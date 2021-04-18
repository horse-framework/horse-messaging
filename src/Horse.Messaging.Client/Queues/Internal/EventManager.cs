using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Internal
{
    internal class EventDescriptor
    {
        public string Queue { get; set; }

        public List<Delegate> Methods { get; set; }

        public Type ParameterType { get; set; }
    }

    internal class EventManager
    {
        private readonly Dictionary<string, List<EventDescriptor>> _descriptors = new Dictionary<string, List<EventDescriptor>>();

        private static readonly string[] EventNames =
        {
            "MessageProduced", "ClientConnected", "ClientDisconnected",
            "Subscribe", "Unsubscribe", "NodeConnected", "NodeDisconnected",
            "QueueCreated", "QueueUpdated", "QueueRemoved"
        };

        public EventManager()
        {
            foreach (string eventName in EventNames)
                _descriptors.Add(eventName, new List<EventDescriptor>());
        }

        internal void Add(string eventName, string queue, Delegate method, Type type)
        {
            List<EventDescriptor> list = _descriptors[eventName];
            lock (list)
            {
                EventDescriptor descriptor = list.FirstOrDefault(x => x.Queue == queue);
                if (descriptor == null)
                {
                    descriptor = new EventDescriptor
                                 {
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

        internal void Remove(string eventName, string queue)
        {
            List<EventDescriptor> list = _descriptors[eventName];
            lock (list)
            {
                EventDescriptor descriptor = list.FirstOrDefault(x => x.Queue == queue);
                if (descriptor == null)
                    return;

                list.Remove(descriptor);
            }
        }

        internal Task TriggerEvents(HorseClient client, HorseMessage message)
        {
            string eventName = message.Source;
            string queue = message.Target;

            List<EventDescriptor> list = _descriptors[eventName];
            EventDescriptor descriptor;

            lock (list)
                descriptor = list.FirstOrDefault(x => x.Queue == queue);

            if (descriptor == null)
                return Task.CompletedTask;

            List<Delegate> methods;
            lock (descriptor.Methods)
                methods = new List<Delegate>(descriptor.Methods);

            object param = message.Deserialize(descriptor.ParameterType, client.MessageSerializer);
            foreach (var m in methods)
                m.DynamicInvoke(param);

            return Task.CompletedTask;
        }
    }
}