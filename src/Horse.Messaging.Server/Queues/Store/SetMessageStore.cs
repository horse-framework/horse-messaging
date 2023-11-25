using System.Collections.Generic;
using System.Data;
using System.Linq;
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

    private HashSet<QueueMessage> _messages = new(new MessageEqualityComparer());

    /// <summary>
    /// Creates new dictionary based message store
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
        }
    }

    public QueueMessage ReadFirst()
    {
        lock (_messages)
        {
            if (_messages.Count == 0)
                return null;

            return _messages.FirstOrDefault();
        }
    }

    public QueueMessage ConsumeFirst()
    {
        lock (_messages)
        {
            if (_messages.Count == 0)
                return null;

            QueueMessage message = _messages.FirstOrDefault();
            _messages.Remove(message);
            return message;
        }
    }

    public QueueMessage Find(string messageId)
    {
        lock (_messages)
            return _messages.FirstOrDefault(x => x.Message.MessageId == messageId);
    }

    public List<QueueMessage> ConsumeMultiple(int count)
    {
        List<QueueMessage> list = new List<QueueMessage>(count);

        lock (_messages)
        {
            for (int i = 0; i < count; i++)
            {
                if (_messages.Count == 0)
                    break;

                QueueMessage message = _messages.FirstOrDefault();
                if (message == null)
                    continue;

                message.IsInQueue = false;

                list.Add(message);
                _messages.Remove(message);
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
            QueueMessage message = _messages.FirstOrDefault(x => x.Message.MessageId == messageId);
            if (message == null)
                return false;

            _messages.Remove(message);
        }

        return true;
    }

    public void Remove(HorseMessage message)
    {
        lock (_messages)
        {
            QueueMessage msg = _messages.FirstOrDefault(x => x.Message.MessageId == message.MessageId);
            if (msg != null)
                _messages.Remove(msg);
        }
    }

    public void Remove(QueueMessage message)
    {
        lock (_messages)
        {
            _messages.Remove(message);
        }
    }

    public Task Clear()
    {
        lock (_messages)
            _messages.Clear();

        return Task.CompletedTask;
    }

    public Task Destroy()
    {
        Clear();
        return Task.CompletedTask;
    }
}