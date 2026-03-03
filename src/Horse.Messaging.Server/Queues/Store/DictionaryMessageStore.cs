using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues.Managers;

namespace Horse.Messaging.Server.Queues.Store;

/// <summary>
/// Dictionary based message store with guaranteed insertion order.
/// Uses OrderedDictionary&lt;TKey,TValue&gt; (.NET 9+) for O(1) indexed access.
/// </summary>
public class DictionaryMessageStore : IQueueMessageStore
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

    private readonly OrderedDictionary<string, QueueMessage> _messages = new();

    /// <summary>
    /// Creates new dictionary based message store
    /// </summary>
    public DictionaryMessageStore(IHorseQueueManager manager)
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
    public void Put(QueueMessage message)
    {
        try
        {
            lock (_messages)
            {
                if (message.IsInQueue)
                    return;

                message.IsInQueue = true;
                _messages.Add(message.Message.MessageId, message);
            }
        }
        catch (ArgumentException)
        {
            throw new DuplicateNameException("Another message with same id is already in queue");
        }
    }

    /// <inheritdoc />
    public QueueMessage ReadFirst()
    {
        lock (_messages)
        {
            if (_messages.Count == 0)
                return null;

            return _messages.GetAt(0).Value;
        }
    }

    /// <inheritdoc />
    public QueueMessage ConsumeFirst()
    {
        lock (_messages)
        {
            if (_messages.Count == 0)
                return null;

            QueueMessage message = _messages.GetAt(0).Value;
            message.IsInQueue = false;
            _messages.RemoveAt(0);
            return message;
        }
    }

    /// <inheritdoc />
    public QueueMessage ConsumeLast()
    {
        lock (_messages)
        {
            if (_messages.Count == 0)
                return null;

            int lastIndex = _messages.Count - 1;
            QueueMessage message = _messages.GetAt(lastIndex).Value;
            message.IsInQueue = false;
            _messages.RemoveAt(lastIndex);
            return message;
        }
    }

    /// <inheritdoc />
    public QueueMessage Find(string messageId)
    {
        lock (_messages)
        {
            _messages.TryGetValue(messageId, out QueueMessage msg);
            return msg;
        }
    }

    /// <inheritdoc />
    public List<QueueMessage> ConsumeMultiple(int count)
    {
        List<QueueMessage> list = new List<QueueMessage>(count);

        lock (_messages)
        {
            int toConsume = Math.Min(count, _messages.Count);
            for (int i = 0; i < toConsume; i++)
            {
                QueueMessage message = _messages.GetAt(0).Value;
                message.IsInQueue = false;
                list.Add(message);
                _messages.RemoveAt(0);
            }
        }

        return list;
    }

    /// <inheritdoc />
    public IEnumerable<QueueMessage> GetUnsafe()
    {
        foreach (QueueMessage message in _messages.Values)
            yield return message;
    }

    /// <inheritdoc />
    public virtual bool Remove(string messageId)
    {
        lock (_messages)
            return _messages.Remove(messageId);
    }

    /// <inheritdoc />
    public virtual void Remove(HorseMessage message)
    {
        lock (_messages)
            _messages.Remove(message.MessageId);
    }

    /// <inheritdoc />
    public virtual void Remove(QueueMessage message)
    {
        lock (_messages)
            _messages.Remove(message.Message.MessageId);
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