using System;
using System.Collections.Generic;

namespace Horse.Messaging.Server.Queues.Store;

public class MessageEqualityComparer : IEqualityComparer<QueueMessage>
{
    public bool Equals(QueueMessage x, QueueMessage y)
    {
        if (x.Message?.MessageId == null) return false;
        if (y.Message?.MessageId == null) return false;

        return x.Message.MessageId.Equals(y.Message.MessageId);
    }

    public int GetHashCode(QueueMessage obj)
    {
        var hashCode = new HashCode();
        hashCode.Add(obj.Message.MessageId);
        return hashCode.ToHashCode();
    }
}