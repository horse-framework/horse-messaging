using System.Collections.Generic;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Server.Queues.Store
{
    /// <summary>
    /// Default non persistent message store.
    /// It keeps messages in linked lists.
    /// </summary>
    public class LinkedMessageStore : IQueueMessageStore
    {
        /// <inheritdoc />
        public IHorseQueueManager Manager { get; }
        
        /// <inheritdoc />
        public IMessageTimeoutTracker TimeoutTracker { get; }

        /// <inheritdoc />
        public bool IsEmpty
        {
            get
            {
                lock (_messages)
                    return _messages.Count == 0;
            }
        }

        private readonly LinkedList<QueueMessage> _messages = new();

        /// <summary>
        /// Creates new linked list message store
        /// </summary>
        public LinkedMessageStore(IHorseQueueManager manager)
        {
            Manager = manager;
            TimeoutTracker = new DefaultMessageTimeoutTracker(manager.Queue, this);
        }

        /// <inheritdoc />
        public int Count()
        {
            return _messages.Count;
        }

        /// <inheritdoc />
        public virtual void Put(QueueMessage message)
        {
            lock (_messages)
            {
                if (message.IsInQueue)
                    return;

                message.IsInQueue = true;
                _messages.AddLast(message);
            }
        }

        /// <inheritdoc />
        public virtual QueueMessage ReadFirst()
        {
            QueueMessage message;
            lock (_messages)
                message = _messages.First?.Value;

            return message;
        }

        /// <inheritdoc />
        public virtual QueueMessage ConsumeFirst()
        {
            lock (_messages)
            {
                if (_messages.Count == 0)
                    return null;

                QueueMessage message;
                message = _messages.First.Value;
                _messages.RemoveFirst();
                message.IsInQueue = false;
                return message;
            }
        }

        /// <inheritdoc />
        public virtual QueueMessage Find(string messageId)
        {
            lock (_messages)
            {
                foreach (QueueMessage qm in _messages)
                {
                    if (qm.Message.MessageId == messageId)
                        return qm;
                }
            }

            return null;
        }

        /// <inheritdoc />
        public virtual List<QueueMessage> ConsumeMultiple(int count)
        {
            List<QueueMessage> list = new List<QueueMessage>(count);

            lock (_messages)
            {
                for (int i = 0; i < count; i++)
                {
                    if (_messages.Count == 0)
                        break;

                    QueueMessage message = _messages.First.Value;
                    if (message == null)
                        continue;

                    list.Add(message);
                    _messages.RemoveFirst();
                }
            }

            return list;
        }

        /// <inheritdoc />
        public IEnumerable<QueueMessage> GetUnsafe()
        {
            foreach (QueueMessage message in _messages)
                yield return message;
        }

        /// <inheritdoc />
        public virtual bool Remove(string messageId)
        {
            lock (_messages)
            {
                LinkedListNode<QueueMessage> node = _messages.First;

                while (node?.Value != null)
                {
                    if (node.Value.Message.MessageId == messageId)
                    {
                        _messages.Remove(node);
                        return true;
                    }

                    node = node.Next;
                }
            }

            return false;
        }

        /// <inheritdoc />
        public virtual void Remove(HorseMessage message)
        {
            lock (_messages)
            {
                LinkedListNode<QueueMessage> node = _messages.First;
                while (node?.Value != null)
                {
                    if (node.Value.Message == message)
                    {
                        _messages.Remove(node);
                        return;
                    }

                    node = node.Next;
                }
            }
        }

        /// <inheritdoc />
        public virtual void Remove(QueueMessage message)
        {
            lock (_messages)
                _messages.Remove(message);
        }

        /// <inheritdoc />
        public virtual Task Clear()
        {
            lock (_messages)
                _messages.Clear();
            
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public virtual Task Destroy()
        {
            Clear();
            return Task.CompletedTask;
        }
    }
}