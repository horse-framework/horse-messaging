using System;
using Horse.Messaging.Client.Internal;

namespace Horse.Messaging.Client.Channels;

internal class ChannelSubscriberRegistration
{
    public string Name { get; set; }

    public Type SubscriberType { get; set; }
    public Type MessageType { get; set; }

    internal ExecutorBase Executer { get; set; }
    internal dynamic Filter { get; set; }
}