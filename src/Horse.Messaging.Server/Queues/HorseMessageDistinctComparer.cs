using System;
using System.Collections.Generic;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Server.Queues
{
    internal class HorseMessageDistinctComparer : IEqualityComparer<HorseMessage>
    {
        public bool Equals(HorseMessage? x, HorseMessage? y)
        {
            return x.MessageId == y.MessageId;
        }

        public int GetHashCode(HorseMessage obj)
        {
            var hashCode = new HashCode();
            hashCode.Add(obj.MessageId);
            return hashCode.ToHashCode();
        }
    }
}