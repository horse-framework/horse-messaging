using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Events;

namespace Horse.Messaging.Client.Events;

/// <summary>
/// Event manager object for horse client
/// </summary>
public class EventOperator
{
    internal HorseClient Client { get; }

    private readonly object _registrationLock = new();
    private EventSubscriberRegistration[] _registrations = [];

    /// <summary>
    /// Registered event handlers. Copy-on-write: readers get an immutable snapshot
    /// and need no lock while handlers are registered concurrently.
    /// </summary>
    internal IReadOnlyList<EventSubscriberRegistration> Registrations => Volatile.Read(ref _registrations);

    internal EventOperator(HorseClient client)
    {
        Client = client;
    }

    internal void AddRegistration(EventSubscriberRegistration registration)
    {
        lock (_registrationLock)
        {
            EventSubscriberRegistration[] current = _registrations;
            EventSubscriberRegistration[] next = new EventSubscriberRegistration[current.Length + 1];
            Array.Copy(current, next, current.Length);
            next[^1] = registration;
            Volatile.Write(ref _registrations, next);
        }
    }

    internal async Task OnEventMessage(HorseMessage message)
    {
        EventSubscriberRegistration[] registrations = Volatile.Read(ref _registrations);
        HorseEvent horseEvent = null;

        // Every matching handler receives the event: handlers for the event target
        // and handlers registered without a target (all targets).
        foreach (EventSubscriberRegistration reg in registrations)
        {
            if ((ushort) reg.Type != message.ContentType)
                continue;

            if (!string.IsNullOrEmpty(reg.Target) && reg.Target != message.Target)
                continue;

            try
            {
                horseEvent ??= (HorseEvent) Client.MessageSerializer.Deserialize(message, typeof(HorseEvent));
                await reg.Executer.Execute(Client, message, horseEvent, Client.ConsumeToken);
            }
            catch (Exception ex)
            {
                Client.OnException(ex, message);
            }
        }
    }

    /// <summary>
    /// Subscribes to an event.
    /// </summary>
    /// <param name="eventType">Type of event to subscribe to.</param>
    /// <param name="target">Event target (e.g. queue name, channel name).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Subscribe(HorseEventType eventType, string target, CancellationToken cancellationToken = default)
    {
        return Subscribe(eventType, target, false, cancellationToken);
    }

    /// <summary>
    /// Subscribes to an event with response verification.
    /// </summary>
    /// <param name="eventType">Type of event to subscribe to.</param>
    /// <param name="target">Event target (e.g. queue name, channel name).</param>
    /// <param name="verifyResponse">If true, waits for the server to confirm the subscription.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Subscribe(HorseEventType eventType, string target, bool verifyResponse, CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage();
        message.Type = MessageType.Event;
        message.ContentType = (ushort) eventType;
        message.SetTarget(target);
        message.WaitResponse = verifyResponse;

        if (verifyResponse)
            message.SetMessageId(Client.UniqueIdGenerator.Create());

        return Client.WaitResponse(message, verifyResponse, cancellationToken);
    }

    /// <summary>
    /// Unsubscribes from an event.
    /// </summary>
    /// <param name="eventType">Type of event to unsubscribe from.</param>
    /// <param name="target">Event target (e.g. queue name, channel name).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Unsubscribe(HorseEventType eventType, string target, CancellationToken cancellationToken = default)
    {
        return Unsubscribe(eventType, target, false, cancellationToken);
    }

    /// <summary>
    /// Unsubscribes from an event with response verification.
    /// </summary>
    /// <param name="eventType">Type of event to unsubscribe from.</param>
    /// <param name="target">Event target (e.g. queue name, channel name).</param>
    /// <param name="verifyResponse">If true, waits for the server to confirm the unsubscription.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<HorseResult> Unsubscribe(HorseEventType eventType, string target, bool verifyResponse, CancellationToken cancellationToken = default)
    {
        HorseMessage message = new HorseMessage();
        message.Type = MessageType.Event;
        message.ContentType = (ushort) eventType;
        message.SetTarget(target);
        message.AddHeader(HorseHeaders.SUBSCRIBE, "No");
        message.WaitResponse = verifyResponse;

        if (verifyResponse)
            message.SetMessageId(Client.UniqueIdGenerator.Create());

        return Client.WaitResponse(message, verifyResponse, cancellationToken);
    }
}