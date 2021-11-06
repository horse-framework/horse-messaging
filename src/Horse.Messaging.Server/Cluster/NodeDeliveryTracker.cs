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

        }

        public void Run()
        {
            _thread.IsBackground = true;
            _thread.Priority = ThreadPriority.BelowNormal;
            
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
                    delivery.IsCommitted = false;

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

        public bool Commit(string messageId)
        {
            NodeMessageDelivery delivery = null;
            
            lock (_deliveries)
            {
                _deliveries.TryGetValue(messageId, out delivery);
                
                if (delivery != null)
                    _deliveries.Remove(messageId);
            }

            if (delivery == null)
                return false;

            delivery.IsCommitted = true;
            delivery.CompletionSource.SetResult(delivery);
            return true;
        }

        public void Untrack(string messageId)
        {
            lock (_deliveries)
                _deliveries.Remove(messageId);
        }
    }
}