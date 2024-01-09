using System;
using System.Collections.Generic;
using System.Threading;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Events;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Containers;

namespace Horse.Messaging.Server.Events;

/// <summary>
/// Manages event subscribers and triggering actions
/// </summary>
public class EventManager : IDisposable
{
    /// <summary>
    /// Event subscribers
    /// </summary>
    public ArrayContainer<MessagingClient> Subscribers { get; } = new();

    /// <summary>
    /// Horse event type
    /// </summary>
    public HorseEventType Type { get; }

    /// <summary>
    /// Event target name (Queue name)
    /// </summary>
    public string Target { get; }

    private Timer _cleanup;
    private readonly HorseRider _server;

    /// <summary>
    /// Name is definition of the event.
    /// Target is the queue name of the event.
    /// Content Type is the Queue Id of the event.
    /// </summary>
    internal EventManager(HorseRider server, HorseEventType type, string target = null)
    {
        Type = type;
        Target = target;
        _server = server;
        _cleanup = new Timer(_ => Subscribers.RemoveAll(p => p == null || !p.IsConnected), null, 60000, 60000);
    }

    /// <summary>
    /// Clears all subscriptions
    /// </summary>
    public void Dispose()
    {
        Subscribers.Clear();
        _cleanup.Dispose();
        _cleanup = null;
    }

    /// <summary>
    /// Triggers event and sends message to subscribers
    /// </summary>
    internal void Trigger(MessagingClient subject, params KeyValuePair<string, string>[] parameters)
    {
        Trigger(subject, null, parameters);
    }

    /// <summary>
    /// Triggers event and sends message to subscribers
    /// </summary>
    internal void Trigger(MessagingClient subject, string target, params KeyValuePair<string, string>[] parameters)
    {
        EventSubject sub = null;
        if (subject != null)
            sub = new EventSubject
            {
                Id = subject.UniqueId,
                Name = subject.Name,
                Type = subject.Type
            };

        Trigger(sub, target, parameters);
    }

    /// <summary>
    /// Triggers event and sends message to subscribers
    /// </summary>
    internal void Trigger(EventSubject subject, params KeyValuePair<string, string>[] parameters)
    {
        Trigger(subject, null, parameters);
    }

    /// <summary>
    /// Triggers event and sends message to subscribers
    /// </summary>
    internal void Trigger(string target, params KeyValuePair<string, string>[] parameters)
    {
        Trigger((EventSubject) null, target, parameters);
    }

    /// <summary>
    /// Triggers event and sends message to subscribers
    /// </summary>
    internal void Trigger(params KeyValuePair<string, string>[] parameters)
    {
        Trigger((EventSubject) null, null, parameters);
    }

    /// <summary>
    /// Triggers event and sends message to subscribers
    /// </summary>
    internal void Trigger(EventSubject subject, string target, params KeyValuePair<string, string>[] parameters)
    {
        if (Subscribers.Count() == 0)
            return;

        try
        {
            HorseEvent e = new HorseEvent
            {
                Type = Type,
                Target = target,
                Subject = subject,
                Parameters = parameters
            };

            HorseMessage message = new HorseMessage(MessageType.Event, target, Convert.ToUInt16(Type));
            message.Serialize(e, _server.MessageContentSerializer);
            byte[] data = HorseProtocolWriter.Create(message);

            foreach (MessagingClient subscriber in Subscribers.All())
            {
                if (subscriber.IsConnected)
                    _ = subscriber.SendRawAsync(data);
            }
        }
        catch (Exception e)
        {
            _server.SendError("EVENT_TRIGGER", e, $"Type:{Type}, Target:{Target}");
        }
    }
}