using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues.Managers;

namespace Horse.Messaging.Server.Queues.Store;

/// <summary>
/// Dictionary based message store
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

    private readonly Dictionary<string, QueueMessage> _messages = new();

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

            return _messages.FirstOrDefault().Value;
        }
    }

    /// <inheritdoc />
    public QueueMessage ConsumeFirst()
    {
        lock (_messages)
        {
            if (_messages.Count == 0)
                return null;

            var keyValue = _messages.FirstOrDefault();
            QueueMessage message = keyValue.Value;
            message.IsInQueue = false;
            _messages.Remove(keyValue.Key);
            return message;
        }
    }

    /// <inheritdoc />
    public QueueMessage Find(string messageId)
    {
        QueueMessage msg;

        lock (_messages)
            _messages.TryGetValue(messageId, out msg);

        return msg;
    }

    /// <inheritdoc />
    public List<QueueMessage> ConsumeMultiple(int count)
    {
        List<QueueMessage> list = new List<QueueMessage>(count);

        lock (_messages)
        {
            for (int i = 0; i < count; i++)
            {
                if (_messages.Count == 0)
                    break;

                QueueMessage message = _messages.FirstOrDefault().Value;
                if (message == null)
                    continue;

                message.IsInQueue = false;
                
                list.Add(message);
                _messages.Remove(message.Message.MessageId);
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
            _messages.Remove(messageId);

        return true;
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