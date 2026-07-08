using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues.Managers;

namespace Horse.Messaging.Server.Queues.Store;

/// <summary>
/// Default non persistent message store.
/// It keeps messages in linked lists.
/// </summary>
public class LinkedMessageStore : IQueueMessageStore
{
    private readonly Lock _sync = new();
    private readonly LinkedList<QueueMessage> _messages = new();
    private readonly Dictionary<string, LinkedListNode<QueueMessage>> _index = new(StringComparer.Ordinal);
    private int _count;

    /// <inheritdoc />
    public IHorseQueueManager Manager { get; }

    /// <inheritdoc />
    public IMessageTimeoutTracker TimeoutTracker { get; }

    /// <inheritdoc />
    public bool IsEmpty
    {
        get => Volatile.Read(ref _count) == 0;
    }

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
        return Volatile.Read(ref _count);
    }

    /// <inheritdoc />
    public virtual void Put(QueueMessage message)
    {
        lock (_sync)
        {
            if (message.IsInQueue)
                return;

            message.IsInQueue = true;
            var node = _messages.AddLast(message);
            _count++;
            if (message.Message.MessageId != null)
                _index[message.Message.MessageId] = node;
        }
    }

    /// <inheritdoc />
    public virtual QueueMessage ReadFirst()
    {
        lock (_sync)
            return _messages.First?.Value;
    }

    /// <inheritdoc />
    public virtual QueueMessage ConsumeFirst()
    {
        lock (_sync)
        {
            if (_messages.First == null)
                return null;

            QueueMessage message = _messages.First.Value;
            _messages.RemoveFirst();
            message.IsInQueue = false;
            _count--;
            if (message.Message.MessageId != null)
                _index.Remove(message.Message.MessageId);
            return message;
        }
    }

    /// <inheritdoc />
    public virtual QueueMessage ConsumeLast()
    {
        lock (_sync)
        {
            if (_messages.Last == null)
                return null;

            QueueMessage message = _messages.Last.Value;
            _messages.RemoveLast();
            message.IsInQueue = false;
            _count--;
            if (message.Message.MessageId != null)
                _index.Remove(message.Message.MessageId);
            return message;
        }
    }

    /// <inheritdoc />
    public virtual QueueMessage Find(string messageId)
    {
        lock (_sync)
        {
            if (_index.TryGetValue(messageId, out var node))
                return node.Value;
        }

        return null;
    }

    /// <inheritdoc />
    public virtual List<QueueMessage> ConsumeMultiple(int count)
    {
        List<QueueMessage> list = new List<QueueMessage>(count);

        lock (_sync)
        {
            for (int i = 0; i < count; i++)
            {
                if (_messages.First == null)
                    break;

                QueueMessage message = _messages.First.Value;
                message.IsInQueue = false;
                if (message.Message.MessageId != null)
                    _index.Remove(message.Message.MessageId);

                list.Add(message);
                _messages.RemoveFirst();
                _count--;
            }
        }

        return list;
    }

    /// <inheritdoc />
    public IEnumerable<QueueMessage> GetUnsafe()
    {
        return _messages;
    }

    /// <inheritdoc />
    public virtual bool Remove(string messageId)
    {
        lock (_sync)
        {
            if (_index.Remove(messageId, out var node))
            {
                RemoveNode(node);
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public virtual void Remove(HorseMessage message)
    {
        lock (_sync)
        {
            if (message.MessageId != null &&
                _index.TryGetValue(message.MessageId, out var node) &&
                node.Value?.Message == message)
            {
                _index.Remove(message.MessageId);
                RemoveNode(node);
                return;
            }

            // Fallback: reference scan
            LinkedListNode<QueueMessage> current = _messages.First;
            while (current?.Value != null)
            {
                if (current.Value.Message == message)
                {
                    if (message.MessageId != null)
                        _index.Remove(message.MessageId);

                    RemoveNode(current);
                    return;
                }

                current = current.Next;
            }
        }
    }

    /// <inheritdoc />
    public virtual void Remove(QueueMessage message)
    {
        lock (_sync)
        {
            if (message.Message.MessageId != null &&
                _index.TryGetValue(message.Message.MessageId, out var node) &&
                node.Value == message)
            {
                _index.Remove(message.Message.MessageId);
                RemoveNode(node);
                return;
            }

            if (_messages.Remove(message))
            {
                if (message.Message.MessageId != null)
                    _index.Remove(message.Message.MessageId);

                message.IsInQueue = false;
                _count--;
            }
        }
    }

    /// <inheritdoc />
    public virtual Task Clear()
    {
        lock (_sync)
        {
            foreach (QueueMessage message in _messages)
                message.IsInQueue = false;

            _messages.Clear();
            _index.Clear();
            _count = 0;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task Destroy()
    {
        Clear();
        return Task.CompletedTask;
    }

    private void RemoveNode(LinkedListNode<QueueMessage> node)
    {
        _messages.Remove(node);
        node.Value.IsInQueue = false;
        _count--;
    }
}
