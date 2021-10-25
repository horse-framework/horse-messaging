using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Server.Cluster
{
    internal class NodeDeliveryTracker
    {
        private readonly Thread _thread;
        private readonly NodeClient _client;
        private readonly Dictionary<string, NodeMessageDelivery> _deliveries = new Dictionary<string, NodeMessageDelivery>();

        public NodeDeliveryTracker(NodeClient client)
        {
            _client = client;
            _thread = new Thread(() =>
            {
                while (true)
                {
                    Thread.Sleep(1000);
                    try
                    {
                        CheckTimeouts();
                    }
                    catch
                    {
                    }
                }
            });

            _thread.IsBackground = true;
            _thread.Priority = ThreadPriority.BelowNormal;
        }

        public void Run()
        {
            _thread.Start();
        }

        private void CheckTimeouts()
        {
            List<NodeMessageDelivery> removing = new List<NodeMessageDelivery>();

            lock (_deliveries)
            {
                if (_deliveries.Count == 0)
                    return;

                foreach (NodeMessageDelivery delivery in _deliveries.Values.Where(x => x.Expiration < DateTime.UtcNow))
                    removing.Add(delivery);

                foreach (NodeMessageDelivery delivery in removing)
                    _deliveries.Remove(delivery.Message.MessageId);
            }

            if (removing.Count > 0)
            {
                foreach (NodeMessageDelivery delivery in removing)
                {
                    delivery.IsTimedOut = true;

                    if (!delivery.CompletionSource.Task.IsCompleted)
                        delivery.CompletionSource.SetResult(delivery);
                }
            }
        }

        public TaskCompletionSource<NodeMessageDelivery> Track(HorseMessage message)
        {
            NodeMessageDelivery messageDelivery = new NodeMessageDelivery
            {
                IsCommitted = false,
                Expiration = DateTime.UtcNow.AddSeconds(5),
                Message = message,
                CompletionSource = new TaskCompletionSource<NodeMessageDelivery>()
            };

            lock (_deliveries)
                _deliveries.Add(message.MessageId, messageDelivery);

            return messageDelivery.CompletionSource;
        }

        public void RemoveCommited(NodeMessageDelivery delivery)
        {
            lock (_deliveries)
                _deliveries.Remove(delivery.Message.MessageId);
        }
    }
}