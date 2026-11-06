using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Horse.Messaging.Protocol.Events;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Containers;

namespace Horse.Messaging.Server.Events;

/// <summary>
/// Subscriber list of an event type and target.
/// Subscriptions of rider level events and channel publish events are owned by their event manager,
/// channel publish subscriptions end with the channel.
/// Subscriptions of queue and router events are kept in the
/// <see cref="EventSubscriptionRegistry"/>, so they survive removing and re-creating the target.
/// </summary>
internal sealed class EventSubscription
{
    internal HorseEventType Type { get; }
    internal string Target { get; }

    /// <summary>
    /// Registry that keeps the subscription, null if the subscription is owned by an event manager
    /// </summary>
    internal EventSubscriptionRegistry Registry { get; }

    /// <summary>
    /// Subscribers. Readers (event publish) iterate it without locking.
    /// Changed together with the clients' own subscription lists, under the registry lock
    /// or, for a subscription owned by an event manager, under <see cref="_sync"/>.
    /// </summary>
    internal ArrayContainer<MessagingClient> Clients { get; } = new();

    /// <summary>
    /// Guards a subscription owned by an event manager (no registry).
    /// Used only by subscribe, unsubscribe and release, never by event publish.
    /// </summary>
    private readonly object _sync = new();

    private volatile bool _released;

    /// <summary>
    /// True after the subscription is removed from its registry or released by its event manager.
    /// A released subscription receives no new subscribers.
    /// </summary>
    internal bool Released => _released;

    internal EventSubscription(HorseEventType type, string target, EventSubscriptionRegistry registry)
    {
        Type = type;
        Target = target;
        Registry = registry;
    }

    internal void MarkReleased() => _released = true;

    /// <summary>
    /// Adds the client to a subscription owned by an event manager.
    /// Returns false if the subscription is released.
    /// </summary>
    internal bool AddOwned(MessagingClient client)
    {
        lock (_sync)
        {
            if (_released)
                return false;

            Clients.Add(client, c => c == client);
            client.AddEventSubscription(this);
            return true;
        }
    }

    /// <summary>
    /// Releases a subscription owned by an event manager: removes all clients and accepts no new ones
    /// </summary>
    internal void ReleaseOwned()
    {
        lock (_sync)
        {
            _released = true;

            foreach (MessagingClient client in Clients.All())
                client?.RemoveEventSubscription(this);

            Clients.Clear();
        }
    }

    /// <summary>
    /// Removes the client from the subscription and the subscription from the client
    /// </summary>
    internal void Remove(MessagingClient client)
    {
        if (Registry != null)
        {
            Registry.Remove(this, client);
            return;
        }

        lock (_sync)
        {
            Clients.Remove(client);
            client.RemoveEventSubscription(this);
        }
    }

    /// <summary>
    /// Removes disconnected clients
    /// </summary>
    internal void Prune()
    {
        if (Registry != null)
            Registry.Prune(this);
        else
            Clients.RemoveAll(c => c == null || !c.IsConnected);
    }
}

/// <summary>
/// Keeps subscriptions of queue and router events by event type and target name.
/// Event managers of the targets read their subscribers from here, so a subscription is not lost
/// when the target is removed and created again with the same name.
/// A subscription is removed from the registry when its last subscriber leaves.
/// </summary>
internal sealed class EventSubscriptionRegistry
{
    /// <summary>
    /// Guards writes: creating and releasing subscriptions, and changing their clients together with the clients' own lists
    /// </summary>
    private readonly object _lock = new();

    /// <summary>
    /// Read without locking by event managers (Find), written only under <see cref="_lock"/>
    /// </summary>
    private readonly ConcurrentDictionary<(HorseEventType, string), EventSubscription> _items = new();

    private int _version;

    /// <summary>
    /// Incremented each time a subscription is created, after it is added to the registry.
    /// Event managers use it to avoid lookups while they have no subscription.
    /// </summary>
    internal int Version => Volatile.Read(ref _version);

    /// <summary>
    /// Finds the subscription without locking. Called by event managers when they trigger an event
    /// after a new subscription is created anywhere, so it must not wait for subscribe or unsubscribe.
    /// </summary>
    internal EventSubscription Find(HorseEventType type, string target)
    {
        return _items.TryGetValue((type, target), out EventSubscription subscription) ? subscription : null;
    }

    /// <summary>
    /// Subscribes the client. Returns false if the client is disconnected, it is not subscribed then.
    /// </summary>
    internal bool Subscribe(HorseEventType type, string target, MessagingClient client)
    {
        EventSubscription subscription;
        lock (_lock)
        {
            if (!_items.TryGetValue((type, target), out subscription))
            {
                subscription = new EventSubscription(type, target, this);
                _items[(type, target)] = subscription;

                // after the add: a manager that reads the new version finds the subscription
                Interlocked.Increment(ref _version);
            }

            // the client's own list changes under the same lock as the subscription,
            // so a concurrent remove of the same client cannot leave only one side updated
            subscription.Clients.Add(client, c => c == client);
            client.AddEventSubscription(subscription);
        }

        // disconnected while subscribing, after its subscriptions are removed
        if (!client.IsConnected)
        {
            Remove(subscription, client);
            return false;
        }

        return true;
    }

    internal void Unsubscribe(HorseEventType type, string target, MessagingClient client)
    {
        EventSubscription subscription = Find(type, target);
        if (subscription != null)
            Remove(subscription, client);
    }

    internal void Remove(EventSubscription subscription, MessagingClient client)
    {
        lock (_lock)
        {
            subscription.Clients.Remove(client);
            client.RemoveEventSubscription(subscription);
            ReleaseIfEmpty(subscription);
        }
    }

    internal void Prune(EventSubscription subscription)
    {
        lock (_lock)
        {
            subscription.Clients.RemoveAll(c => c == null || !c.IsConnected);
            ReleaseIfEmpty(subscription);
        }
    }

    private void ReleaseIfEmpty(EventSubscription subscription)
    {
        if (subscription.Released || subscription.Clients.Count() > 0)
            return;

        // removes only this instance
        _items.TryRemove(new KeyValuePair<(HorseEventType, string), EventSubscription>((subscription.Type, subscription.Target), subscription));

        // managers that cached it see the flag and look up again
        subscription.MarkReleased();
    }
}
