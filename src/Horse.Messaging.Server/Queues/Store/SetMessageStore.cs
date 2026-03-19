using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues.Managers;

namespace Horse.Messaging.Server.Queues.Store;

public class SetMessageStore : IQueueMessageStore
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

    private readonly HashSet<QueueMessage> _messages = new(new MessageEqualityComparer());
    private readonly Dictionary<string, QueueMessage> _index = new();

    /// <summary>
    /// Creates new set based message store
    /// </summary>
    public SetMessageStore(IHorseQueueManager manager)
    {
        Manager = manager;
        TimeoutTracker = new DefaultMessageTimeoutTracker(manager.Queue, this);
    }

    public int Count()
    {
        return _messages.Count;
    }

    public void Put(QueueMessage message)
    {
        lock (_messages)
        {
            if (message.IsInQueue)
                return;

            message.IsInQueue = true;
            bool added = _messages.Add(message);
            if (!added)
                throw new DuplicateNameException("Another message with same id is already in queue");

            if (message.Message.MessageId != null)
                _index[message.Message.MessageId] = message;
        }
    }

    public QueueMessage ReadFirst()
    {
        lock (_messages)
        {
            if (_messages.Count == 0)
                return null;

            using var enumerator = _messages.GetEnumerator();
            return enumerator.MoveNext() ? enumerator.Current : null;
        }
    }

    public QueueMessage ConsumeFirst()
    {
        lock (_messages)
        {
            if (_messages.Count == 0)
                return null;

            using var enumerator = _messages.GetEnumerator();
            if (!enumerator.MoveNext())
                return null;

            QueueMessage message = enumerator.Current;
            _messages.Remove(message);
            if (message.Message.MessageId != null)
                _index.Remove(message.Message.MessageId);
            return message;
        }
    }

    public QueueMessage ConsumeLast()
    {
        lock (_messages)
        {
            if (_messages.Count == 0)
                return null;

            // HashSet has no indexed access — enumerate to last element
            QueueMessage last = null;
            foreach (QueueMessage m in _messages)
                last = m;

            if (last != null)
            {
                _messages.Remove(last);
                if (last.Message.MessageId != null)
                    _index.Remove(last.Message.MessageId);
            }

            return last;
        }
    }

    public QueueMessage Find(string messageId)
    {
        lock (_messages)
        {
            _index.TryGetValue(messageId, out QueueMessage msg);
            return msg;
        }
    }

    public List<QueueMessage> ConsumeMultiple(int count)
    {
        List<QueueMessage> list = new List<QueueMessage>(count);

        lock (_messages)
        {
            foreach (QueueMessage message in _messages)
            {
                if (list.Count >= count)
                    break;

                message.IsInQueue = false;
                list.Add(message);
            }

            foreach (QueueMessage message in list)
            {
                _messages.Remove(message);
                if (message.Message.MessageId != null)
                    _index.Remove(message.Message.MessageId);
            }
        }

        return list;
    }

    public IEnumerable<QueueMessage> GetUnsafe()
    {
        foreach (QueueMessage message in _messages)
            yield return message;
    }

    public bool Remove(string messageId)
    {
        lock (_messages)
        {
            if (!_index.Remove(messageId, out QueueMessage message))
                return false;

            _messages.Remove(message);
        }

        return true;
    }

    public void Remove(HorseMessage message)
    {
        lock (_messages)
        {
            if (message.MessageId != null && _index.Remove(message.MessageId, out QueueMessage msg))
                _messages.Remove(msg);
        }
    }

    public void Remove(QueueMessage message)
    {
        lock (_messages)
        {
            _messages.Remove(message);
            if (message.Message.MessageId != null)
                _index.Remove(message.Message.MessageId);
        }
    }

    public Task Clear()
    {
        lock (_messages)
        {
            _messages.Clear();
            _index.Clear();
        }

        return Task.CompletedTask;
    }

    public Task Destroy()
    {
        Clear();
        return Task.CompletedTask;
    }
}