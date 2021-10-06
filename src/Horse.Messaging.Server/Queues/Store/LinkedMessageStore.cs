using System;
using System.Collections.Generic;
using System.Linq;

namespace Horse.Messaging.Server.Queues.Store
{
    internal class LinkedMessageStore : IQueueMessageStore
    {
        private readonly HorseQueue _queue;
        private readonly LinkedList<QueueMessage> _messages = new();
        private readonly LinkedList<QueueMessage> _messagesPrio = new();

        public LinkedMessageStore(HorseQueue queue)
        {
            _queue = queue;
        }

        public int CountAll()
        {
            return _messages.Count + _messagesPrio.Count;
        }

        public int CountRegular()
        {
            return _messages.Count;
        }

        public int CountPriority()
        {
            return _messagesPrio.Count;
        }

        public void Put(QueueMessage message)
        {
            if (message.Message.HighPriority)
            {
                lock (_messagesPrio)
                {
                    if (message.IsInQueue)
                        return;

                    message.IsInQueue = true;
                    _messagesPrio.AddFirst(message);
                }
            }
            else
            {
                lock (_messages)
                {
                    if (message.IsInQueue)
                        return;

                    message.IsInQueue = true;
                    _messages.AddLast(message);
                }
            }
        }

        public IEnumerable<string> GetMessageIdList(bool priorityMessages)
        {
            if (priorityMessages)
            {
                foreach (QueueMessage msg in _messagesPrio)
                    yield return msg.Message.MessageId;
            }
            else
            {
                foreach (QueueMessage msg in _messages)
                    yield return msg.Message.MessageId;
            }
        }

        public QueueMessage GetNext(bool remove, bool fromEnd = false)
        {
            QueueMessage message = GetPriorityNext(remove, fromEnd);

            if (message != null)
                return message;

            return GetRegularNext(remove, fromEnd);
        }

        public QueueMessage GetRegularNext(bool remove, bool fromEnd = false)
        {
            lock (_messages)
            {
                if (_messages.Count == 0)
                    return null;

                QueueMessage message;
                if (fromEnd)
                {
                    message = _messages.Last.Value;
                    if (remove)
                    {
                        _messages.RemoveLast();
                        message.IsInQueue = false;
                    }
                }
                else
                {
                    message = _messages.First.Value;
                    if (remove)
                    {
                        _messages.RemoveFirst();
                        message.IsInQueue = false;
                    }
                }

                return message;
            }
        }

        public QueueMessage GetPriorityNext(bool remove, bool fromEnd = false)
        {
            if (_messagesPrio.Count > 0)
            {
                QueueMessage prioMessage = null;
                lock (_messagesPrio)
                {
                    if (_messagesPrio.Count > 0)
                    {
                        if (fromEnd)
                        {
                            prioMessage = _messagesPrio.Last.Value;
                            if (remove)
                            {
                                _messagesPrio.RemoveLast();
                                prioMessage.IsInQueue = false;
                            }
                        }
                        else
                        {
                            prioMessage = _messagesPrio.First.Value;
                            if (remove)
                            {
                                _messagesPrio.RemoveFirst();
                                prioMessage.IsInQueue = false;
                            }
                        }
                    }
                }

                if (prioMessage != null)
                    return prioMessage;
            }

            return null;
        }

        public QueueMessage FindAndRemove(Func<QueueMessage, bool> predicate)
        {
            lock (_messages)
            {
                foreach (QueueMessage message in _messages)
                {
                    if (predicate(message))
                    {
                        message.IsInQueue = false;
                        _messages.Remove(message);
                        return message;
                    }
                }
            }

            lock (_messagesPrio)
            {
                foreach (QueueMessage message in _messagesPrio)
                {
                    if (predicate(message))
                    {
                        message.IsInQueue = false;
                        _messagesPrio.Remove(message);
                        return message;
                    }
                }
            }

            return null;
        }

        public List<QueueMessage> FindAll(Func<QueueMessage, bool> predicate)
        {
            List<QueueMessage> messages = new List<QueueMessage>();

            lock (_messagesPrio)
                messages.AddRange(_messagesPrio.Where(predicate));

            lock (_messages)
                messages.AddRange(_messages.Where(predicate));

            return messages;
        }

        public List<QueueMessage> FindAndRemoveRegular(Func<QueueMessage, bool> predicate)
        {
            List<QueueMessage> messages = new List<QueueMessage>();

            lock (_messages)
            {
                if (_messages.Count == 0)
                    return messages;

                LinkedListNode<QueueMessage> msg = _messages.First;

                while (msg.Next != null)
                {
                    if (predicate(msg.Value))
                    {
                        msg.Value.IsInQueue = false;
                        LinkedListNode<QueueMessage> next = msg.Next;
                        messages.Add(msg.Value);
                        _messages.Remove(msg);
                        msg = next;
                    }
                }
            }

            return messages;
        }

        public List<QueueMessage> FindAndRemovePriority(Func<QueueMessage, bool> predicate)
        {
            List<QueueMessage> messages = new List<QueueMessage>();

            lock (_messagesPrio)
            {
                if (_messagesPrio.Count == 0)
                    return messages;

                LinkedListNode<QueueMessage> msg = _messagesPrio.First;

                while (msg.Next != null)
                {
                    if (predicate(msg.Value))
                    {
                        msg.Value.IsInQueue = false;
                        LinkedListNode<QueueMessage> next = msg.Next;
                        messages.Add(msg.Value);
                        _messagesPrio.Remove(msg);
                        msg = next;
                    }
                }
            }

            return messages;
        }

        public IEnumerable<QueueMessage> GetUnsafe()
        {
            foreach (QueueMessage message in _messages)
                yield return message;
        }

        public IEnumerable<QueueMessage> GetUnsafePriority()
        {
            foreach (QueueMessage message in _messagesPrio)
                yield return message;
        }

        public void Remove(QueueMessage message)
        {
            if (message.Message.HighPriority)
            {
                lock (_messagesPrio)
                    _messagesPrio.Remove(message);
            }
            else
            {
                lock (_messages)
                    _messages.Remove(message);
            }
        }

        public void ClearRegular()
        {
            lock (_messages)
                _messages.Clear();
        }

        public void ClearPriority()
        {
            lock (_messagesPrio)
                _messagesPrio.Clear();
        }

        public void ClearAll()
        {
            lock (_messages)
                _messages.Clear();

            lock (_messagesPrio)
                _messagesPrio.Clear();
        }
    }
}