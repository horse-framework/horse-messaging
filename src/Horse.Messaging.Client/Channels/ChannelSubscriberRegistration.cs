using System;
using System.Collections.Generic;
using Horse.Messaging.Client.Internal;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Channels;

internal class ChannelSubscriberRegistration
{
    public string Name { get; set; }
    public Type SubscriberType { get; set; }
    public Type MessageType { get; set; }
    internal List<KeyValuePair<string, string>> SubscriptionHeaders { get; } = new();
    internal ExecutorBase Executer { get; set; }
    internal Func<HorseMessage, object, bool> Filter { get; set; }
}
