using System;
using System.Collections.Generic;
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
    private readonly Dictionary<string, LinkedListNode<QueueMessage>> _index = new(StringComparer.Ordinal);

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
            var node = _messages.AddLast(message);
            if (message.Message.MessageId != null)
                _index[message.Message.MessageId] = node;
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

            QueueMessage message = _messages.First.Value;
            _messages.RemoveFirst();
            message.IsInQueue = false;
            if (message.Message.MessageId != null)
                _index.Remove(message.Message.MessageId);
            return message;
        }
    }

    /// <inheritdoc />
    public virtual QueueMessage ConsumeLast()
    {
        lock (_messages)
        {
            if (_messages.Count == 0)
                return null;

            QueueMessage message = _messages.Last.Value;
            _messages.RemoveLast();
            message.IsInQueue = false;
            if (message.Message.MessageId != null)
                _index.Remove(message.Message.MessageId);
            return message;
        }
    }

    /// <inheritdoc />
    public virtual QueueMessage Find(string messageId)
    {
        lock (_messages)
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

        lock (_messages)
        {
            for (int i = 0; i < count; i++)
            {
                if (_messages.Count == 0)
                    break;

                QueueMessage message = _messages.First.Value;
                if (message == null)
                    continue;

                message.IsInQueue = false;
                if (message.Message.MessageId != null)
                    _index.Remove(message.Message.MessageId);

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
            if (_index.Remove(messageId, out var node))
            {
                _messages.Remove(node);
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public virtual void Remove(HorseMessage message)
    {
        lock (_messages)
        {
            if (message.MessageId != null && _index.Remove(message.MessageId, out var node))
            {
                _messages.Remove(node);
                return;
            }

            // Fallback: reference scan
            LinkedListNode<QueueMessage> current = _messages.First;
            while (current?.Value != null)
            {
                if (current.Value.Message == message)
                {
                    _messages.Remove(current);
                    return;
                }

                current = current.Next;
            }
        }
    }

    /// <inheritdoc />
    public virtual void Remove(QueueMessage message)
    {
        if (message.IsInQueue)
            lock (_messages)
            {
                if (message.Message.MessageId != null && _index.Remove(message.Message.MessageId, out var node))
                    _messages.Remove(node);
                else
                    _messages.Remove(message);
            }
    }

    /// <inheritdoc />
    public virtual Task Clear()
    {
        lock (_messages)
        {
            _messages.Clear();
            _index.Clear();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task Destroy()
    {
        Clear();
        return Task.CompletedTask;
    }
}