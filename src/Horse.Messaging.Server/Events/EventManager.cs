using System;
using System.Collections.Generic;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Events;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Logging;

namespace Horse.Messaging.Server.Events;

/// <summary>
/// Manages event subscribers and triggering actions
/// </summary>
public class EventManager : IDisposable
{
    /// <summary>
    /// Event subscribers
    /// </summary>
    public IReadOnlyList<MessagingClient> Subscribers => GetSubscription()?.Clients.All() ?? Array.Empty<MessagingClient>();

    /// <summary>
    /// True if the event has subscribers, here or on the manager the event is forwarded to.
    /// Callers can check it before preparing event parameters.
    /// </summary>
    public bool HasSubscribers
    {
        get
        {
            if (GetSubscription()?.Clients.Count() > 0)
                return true;

            return _forwardTarget != null && ResolveForward()?.GetSubscription()?.Clients.Count() > 0;
        }
    }

    /// <summary>
    /// Horse event type
    /// </summary>
    public HorseEventType Type { get; }

    /// <summary>
    /// Event target name (Queue name)
    /// </summary>
    public string Target { get; }

    private readonly HorseRider _server;

    /// <summary>
    /// Subscription owned by this manager (rider level events and channel publish events).
    /// It ends with the manager.
    /// </summary>
    private readonly EventSubscription _ownSubscription;

    /// <summary>
    /// Registry of subscriptions kept by target name (queue and router events), null if the manager owns its subscription
    /// </summary>
    private readonly EventSubscriptionRegistry _registry;

    /// <summary>
    /// Last registry lookup. Subscription and version are kept in one object
    /// so concurrent lookups cannot store a subscription with another lookup's version.
    /// </summary>
    private sealed class RegistryLookup(EventSubscription subscription, int version)
    {
        public readonly EventSubscription Subscription = subscription;
        public readonly int Version = version;
    }

    private RegistryLookup _lookup;

    private Func<EventManager> _forwardTarget;
    private KeyValuePair<string, string>[] _forwardParameters = Array.Empty<KeyValuePair<string, string>>();

    /// <summary>
    /// Name is definition of the event.
    /// Target is the queue name of the event.
    /// Content Type is the Queue Id of the event.
    /// </summary>
    /// <param name="server">Horse rider</param>
    /// <param name="type">Event type</param>
    /// <param name="target">Target name (queue, channel or router), null for rider level events</param>
    /// <param name="keepSubscriptionsByName">
    /// True to keep the subscriptions of a target event in the rider registry by target name,
    /// so they survive removing and re-creating the target with the same name (queue and router events).
    /// False to end the subscriptions with this manager (channel events).
    /// </param>
    internal EventManager(HorseRider server, HorseEventType type, string target = null, bool keepSubscriptionsByName = true)
    {
        Type = type;
        Target = target;
        _server = server;

        if (keepSubscriptionsByName && !string.IsNullOrEmpty(target) && server != null)
            _registry = server.EventSubscriptions;
        else
            _ownSubscription = new EventSubscription(type, target, null);
    }

    private EventSubscription GetSubscription()
    {
        if (_registry == null)
            return _ownSubscription;

        RegistryLookup lookup = _lookup;
        if (lookup != null)
        {
            EventSubscription cached = lookup.Subscription;
            if (cached != null && !cached.Released)
                return cached;
        }

        // no subscription is created since the last lookup
        int version = _registry.Version;
        if (lookup != null && lookup.Subscription == null && lookup.Version == version)
            return null;

        EventSubscription subscription = _registry.Find(Type, Target);
        _lookup = new RegistryLookup(subscription, version);
        return subscription;
    }

    /// <summary>
    /// Subscribes the client to the event.
    /// The subscription is removed when the client unsubscribes or disconnects.
    /// </summary>
    /// <returns>False if the manager is disposed (its target is removed) or the client is disconnected, the client is not subscribed then</returns>
    public bool Subscribe(MessagingClient client)
    {
        if (client == null)
            return false;

        if (_registry != null)
            return _registry.Subscribe(Type, Target, client);

        // false if the manager is disposed (its target is removed)
        if (!_ownSubscription.AddOwned(client))
            return false;

        // disconnected while subscribing, after its subscriptions are removed
        if (!client.IsConnected)
        {
            _ownSubscription.Remove(client);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Unsubscribes the client from the event
    /// </summary>
    public void Unsubscribe(MessagingClient client)
    {
        if (client == null)
            return;

        if (_registry != null)
        {
            _registry.Unsubscribe(Type, Target, client);
            return;
        }

        _ownSubscription.Remove(client);
    }

    /// <summary>
    /// Every event triggered on this manager is also triggered on the manager <paramref name="target"/> returns,
    /// with its own target and with <paramref name="parameters"/> appended.
    /// Used by partition sub-queues to publish their events on the parent queue.
    /// </summary>
    internal void ForwardTo(Func<EventManager> target, params KeyValuePair<string, string>[] parameters)
    {
        _forwardParameters = parameters ?? Array.Empty<KeyValuePair<string, string>>();
        _forwardTarget = target;
    }

    private EventManager ResolveForward()
    {
        Func<EventManager> forwardTarget = _forwardTarget;
        if (forwardTarget == null)
            return null;

        try
        {
            EventManager forward = forwardTarget();
            return ReferenceEquals(forward, this) ? null : forward;
        }
        catch (Exception ex)
        {
            _server.SendError(HorseLogLevel.Error, HorseLogEvents.EventTrigger, $"Event Forward Type:{Type}, Target:{Target}", ex);
            return null;
        }
    }

    /// <summary>
    /// Releases the event manager.
    /// Subscriptions owned by the manager (rider level events and channel publish events) are removed.
    /// Subscriptions kept by target name (queue and router events) stay for a target created later with the same name,
    /// they are removed when the clients unsubscribe or disconnect.
    /// </summary>
    public void Dispose()
    {
        _forwardTarget = null;
        _ownSubscription?.ReleaseOwned();
    }

    /// <summary>
    /// Triggers event and sends message to subscribers
    /// </summary>
    internal void Trigger(MessagingClient subject)
    {
        Trigger(subject, null, Array.Empty<KeyValuePair<string, string>>());
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
        if (!HasSubscribers)
            return;

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
        Trigger((EventSubject)null, target, parameters);
    }

    /// <summary>
    /// Triggers event and sends message to subscribers (no params, avoids array allocation)
    /// </summary>
    internal void Trigger()
    {
        Trigger((EventSubject)null, null, Array.Empty<KeyValuePair<string, string>>());
    }

    /// <summary>
    /// Triggers event and sends message to subscribers
    /// </summary>
    internal void Trigger(params KeyValuePair<string, string>[] parameters)
    {
        Trigger((EventSubject)null, null, parameters);
    }

    /// <summary>
    /// Triggers event and sends message to subscribers
    /// </summary>
    internal void Trigger(EventSubject subject, string target, params KeyValuePair<string, string>[] parameters)
    {
        Publish(subject, target, parameters);

        if (_forwardTarget == null)
            return;

        EventManager forward = ResolveForward();
        if (forward == null || !(forward.GetSubscription()?.Clients.Count() > 0))
            return;

        // The forwarded event carries the target of the receiving manager (e.g. the parent queue name).
        forward.Publish(subject, null, Append(parameters, _forwardParameters));
    }

    private void Publish(EventSubject subject, string target, KeyValuePair<string, string>[] parameters)
    {
        EventSubscription subscription = GetSubscription();
        if (subscription == null)
            return;

        MessagingClient[] clients = subscription.Clients.All();
        if (clients.Length == 0)
            return;

        // Per-target managers (queue, channel, router) fall back to their own target, so client
        // handlers registered for that target can match the event message.
        target ??= Target;

        byte[] data;
        try
        {
            HorseEvent e = new HorseEvent
            {
                Type = Type,
                Target = target,
                Subject = subject,
                Parameters = parameters
            };

            HorseMessage message = new HorseMessage(MessageType.Event, target ?? string.Empty, (ushort) Type);
            message.Serialize(e, _server.MessageContentSerializer);
            data = HorseProtocolWriter.Create(message);
        }
        catch (Exception ex)
        {
            _server.SendError(HorseLogLevel.Error, HorseLogEvents.EventTrigger, $"Event Trigger Type:{Type}, Target:{target}", ex);
            return;
        }

        // Each client sends its events in order with its own sender.
        // Enqueueing does not wait for the send, a slow client does not delay the others.
        bool hasDisconnected = false;
        foreach (MessagingClient client in clients)
        {
            if (client == null || !client.IsConnected)
            {
                hasDisconnected = true;
                continue;
            }

            client.EnqueueEvent(data);
        }

        // clients normally leave on disconnect, this removes the ones that subscribed while disconnecting
        if (hasDisconnected)
            subscription.Prune();
    }

    private static KeyValuePair<string, string>[] Append(KeyValuePair<string, string>[] first, KeyValuePair<string, string>[] second)
    {
        if (second.Length == 0)
            return first ?? Array.Empty<KeyValuePair<string, string>>();

        if (first == null || first.Length == 0)
            return second;

        KeyValuePair<string, string>[] result = new KeyValuePair<string, string>[first.Length + second.Length];
        Array.Copy(first, result, first.Length);
        Array.Copy(second, 0, result, first.Length, second.Length);
        return result;
    }
}
