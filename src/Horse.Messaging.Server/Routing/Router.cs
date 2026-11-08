using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Plugins;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Events;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Events;
using Horse.Messaging.Server.Logging;

namespace Horse.Messaging.Server.Routing;

/// <summary>
/// Horse MQ Router object.
/// A router, routes messages to its' bindings
/// </summary>
public class Router
{
    #region Properties

    /// <summary>
    /// The server that router is defined
    /// </summary>
    public HorseRider Rider { get; }

    /// <summary>
    /// Route name.
    /// Must be unique.
    /// Can't include " ", "*" or ";"
    /// </summary>
    public string Name { get; }

    private bool _isEnabled = true;

    /// <summary>
    /// If true, messages are routed to bindings.
    /// If false, messages are not routed.
    /// A change is saved and sent to the other nodes.
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetEnabled(value, true);
    }

    /// <summary>
    /// Route method. Defines how messages will be routed.
    /// </summary>
    public RouteMethod Method { get; }

    /// <summary>
    /// Bindings for the router
    /// </summary>
    public Binding[] Bindings { get; private set; } = new Binding[0];

    /// <summary>
    /// Used for round robin routing.
    /// The index value of the binding received last message.
    /// </summary>
    private int _lastRoutedIndex = -1;

    /// <summary>
    /// Event Manage for HorseEventType.MessagePublishedToRouter
    /// </summary>
    public EventManager PublishEvent { get; }

    /// <summary>
    /// Saved bindings whose type could not be loaded. See KeepUnresolvedBinding.
    /// </summary>
    private BindingConfiguration[] _unresolvedBindings = Array.Empty<BindingConfiguration>();

    /// <summary>
    /// Saved bindings whose type could not be loaded. They are saved with the router.
    /// </summary>
    internal BindingConfiguration[] UnresolvedBindings => _unresolvedBindings;

    /// <summary>
    /// Serializes changes of the bindings and the enabled state (cold path).
    /// Bindings are replaced as a new array; two changes at the same time read the same old array and
    /// one of them was lost. Saving and cluster messages are in the lock too, so they keep the order of the changes.
    /// Publishing reads the array without the lock.
    /// </summary>
    private readonly object _changeLock = new();

    #endregion

    /// <summary>
    /// Creates new router on the server
    /// </summary>
    public Router(HorseRider rider, string name, RouteMethod method)
    {
        Rider = rider;
        Name = name;
        Method = method;
        PublishEvent = new EventManager(rider, HorseEventType.RouterPublish, name);
    }

    /// <summary>
    /// Changes the enabled state. The change is saved and sent to the other nodes
    /// when the router is added to the server.
    /// </summary>
    internal void SetEnabled(bool enabled, bool notifyCluster)
    {
        lock (_changeLock)
        {
            if (_isEnabled == enabled)
                return;

            _isEnabled = enabled;

            if (!IsRegistered)
                return;

            Rider.Router.SaveRouterConfiguration(this);

            if (notifyCluster)
                Rider.Router.ClusterNotifier.SendRouterUpdated(this);
        }
    }

    #region Add - Remove

    /// <summary>
    /// Returns all bindings of router
    /// </summary>
    public Binding[] GetBindings()
    {
        return Bindings;
    }

    /// <summary>
    /// Adds new binding to router
    /// </summary>
    public virtual Router AddBinding<T>(Action<Binding> options = null) where T : Binding, new()
    {
        T binding = new T();
        options?.Invoke(binding);
        bool added = AddBinding(binding);
        if (!added)
            throw new InvalidOperationException("An error has occured while attempting to add Binding. Exception message is sent to rider error handler");

        return this;
    }

    /// <summary>
    /// Adds new binding to router
    /// </summary>
    public virtual bool AddBinding<TBinding>(TBinding binding) where TBinding : Binding, new()
    {
        return AddBinding(binding, true);
    }

    /// <summary>
    /// Adds new binding to router
    /// </summary>
    internal virtual bool AddBinding<TBinding>(TBinding binding, bool notifyCluster) where TBinding : Binding, new()
    {
        return AddBinding((Binding)binding, notifyCluster);
    }

    /// <summary>
    /// Adds new binding.
    /// Binding type must have parameterless constructor.
    /// Otherwise an exception is thrown.
    /// </summary>
    /// <exception cref="InvalidOperationException">Binding type must have parameterless constructor</exception>
    public virtual bool AddBinding(Binding binding)
    {
        return AddBinding(binding, true);
    }

    /// <summary>
    /// Adds new binding.
    /// Binding type must have parameterless constructor.
    /// Otherwise an exception is thrown.
    /// </summary>
    /// <exception cref="InvalidOperationException">Binding type must have parameterless constructor</exception>
    internal virtual bool AddBinding(Binding binding, bool notifyCluster)
    {
        Type type = binding.GetType();
        ConstructorInfo[] ctors = type.GetConstructors();
        bool hasParameterlessCtor = ctors.Any(x => x.GetParameters().Length == 0);
        if (!hasParameterlessCtor)
            throw new InvalidOperationException($"Binding type {type.FullName} must have parameterless constructor");

        try
        {
            lock (_changeLock)
            {
                if (Bindings.Any(x => x.Name == binding.Name) || _unresolvedBindings.Any(x => x.Name == binding.Name))
                    return false;

                List<Binding> list = Bindings.ToList();
                list.Add(binding);

                binding.Router = this;
                Bindings = list.OrderByDescending(x => x.Priority).ToArray();
                Rider.Router.BindingAddEvent.Trigger(Name, new KeyValuePair<string, string>("Binding-Name", binding.Name));

                // A router that is not added to the server yet is saved and sent to the other nodes when it is added.
                // Saving it here left an entry for a router that was never added, or replaced the saved
                // configuration of another router with the same name.
                if (IsRegistered)
                {
                    Rider.Router.SaveRouterConfiguration(this);

                    if (notifyCluster)
                        Rider.Router.ClusterNotifier.SendRouterAddBinding(this, binding);
                }

                return true;
            }
        }
        catch (Exception e)
        {
            Rider.SendError(HorseLogLevel.Error, HorseLogEvents.RouterBindingAdd, $"AddBinding Router:{Name},  Binding:{binding?.Name}", e);
            return false;
        }
    }

    /// <summary>
    /// Adds a binding created from a saved configuration (startup or a create message of another node).
    /// No events, saves or cluster messages: the router is not added to the server yet.
    /// </summary>
    internal bool LoadBinding(Binding binding)
    {
        lock (_changeLock)
        {
            if (Bindings.Any(x => x.Name == binding.Name))
                return false;

            binding.Router = this;
            Bindings = Bindings.Append(binding).OrderByDescending(x => x.Priority).ToArray();
            return true;
        }
    }

    /// <summary>
    /// Keeps a saved binding whose type could not be loaded (a custom binding type that is not available).
    /// It is not used for routing, but it is saved with the router, so a later save does not delete it.
    /// </summary>
    internal void KeepUnresolvedBinding(BindingConfiguration configuration)
    {
        if (configuration == null)
            return;

        lock (_changeLock)
        {
            if (configuration.Name != null && (Bindings.Any(x => x.Name == configuration.Name) || _unresolvedBindings.Any(x => x.Name == configuration.Name)))
                return;

            _unresolvedBindings = _unresolvedBindings.Append(configuration).ToArray();
        }
    }

    /// <summary>
    /// Removes a kept binding configuration. Called in the change lock.
    /// </summary>
    private bool RemoveUnresolvedBinding(string bindingName)
    {
        BindingConfiguration[] current = _unresolvedBindings;
        BindingConfiguration[] remaining = current.Where(x => x.Name != bindingName).ToArray();
        if (remaining.Length == current.Length)
            return false;

        _unresolvedBindings = remaining;
        return true;
    }

    /// <summary>
    /// True if this router object is the one added to the server with its name
    /// </summary>
    private bool IsRegistered => Name != null && ReferenceEquals(Rider.Router.Find(Name), this);

    /// <summary>
    /// Removes a binding from the route
    /// </summary>
    public virtual void RemoveBinding(string bindingName)
    {
        RemoveBinding(bindingName, true);
    }

    /// <summary>
    /// Removes a binding from the route
    /// </summary>
    internal virtual void RemoveBinding(string bindingName, bool notifyCluster)
    {
        try
        {
            lock (_changeLock)
            {
                Binding binding = Bindings.FirstOrDefault(x => x.Name == bindingName);
                if (binding == null)
                {
                    // A saved binding whose type could not be loaded can be removed by its name too
                    if (RemoveUnresolvedBinding(bindingName) && IsRegistered)
                    {
                        Rider.Router.SaveRouterConfiguration(this);

                        if (notifyCluster)
                            Rider.Router.ClusterNotifier.SendRouterRemoveBinding(this, bindingName);
                    }

                    return;
                }

                RemoveFromBindings(binding, notifyCluster);
            }
        }
        catch (Exception e)
        {
            Rider.SendError(HorseLogLevel.Error, HorseLogEvents.RouterBindingRemove, $"RemoveBinding Router:{Name},  Binding:{bindingName}", e);
        }
    }

    /// <summary>
    /// Removes a binding from the route
    /// </summary>
    public virtual void RemoveBinding(Binding binding)
    {
        RemoveBinding(binding, true);
    }

    /// <summary>
    /// Removes a binding from the route
    /// </summary>
    internal virtual void RemoveBinding(Binding binding, bool notifyCluster)
    {
        try
        {
            if (binding == null)
                return;

            lock (_changeLock)
            {
                if (!Bindings.Contains(binding))
                    return;

                RemoveFromBindings(binding, notifyCluster);
            }
        }
        catch (Exception e)
        {
            Rider.SendError(HorseLogLevel.Error, HorseLogEvents.RouterBindingRemove, $"RemoveBinding Router:{Name},  Binding:{binding?.Name}", e);
        }
    }

    /// <summary>
    /// Removes the binding, saves the router and notifies the other nodes. Called in the change lock.
    /// The binding keeps its Router: a message that is being sent by the binding still uses it,
    /// and setting it to null made that send fail with a NullReferenceException.
    /// </summary>
    private void RemoveFromBindings(Binding binding, bool notifyCluster)
    {
        Bindings = Bindings.Where(x => !ReferenceEquals(x, binding)).ToArray();
        Rider.Router.BindingRemoveEvent.Trigger(Name, new KeyValuePair<string, string>("Binding-Name", binding.Name));

        if (IsRegistered)
        {
            Rider.Router.SaveRouterConfiguration(this);

            if (notifyCluster)
                Rider.Router.ClusterNotifier.SendRouterRemoveBinding(this, binding.Name);
        }
    }

    #endregion

    #region Publish

    /// <summary>
    /// The values of the published message that bindings change (a direct binding changes the type,
    /// target, content type and acknowledge flag of the message it sends).
    /// Every binding after the first starts from the values the producer sent.
    /// </summary>
    private readonly struct MessageState
    {
        private readonly MessageType _type;
        private readonly string _target;
        private readonly ushort _contentType;
        private readonly bool _waitResponse;

        public MessageState(HorseMessage message)
        {
            _type = message.Type;
            _target = message.Target;
            _contentType = message.ContentType;
            _waitResponse = message.WaitResponse;
        }

        /// <summary>
        /// Writes the producer's values back before the next binding.
        /// Never called after the binding that accepted the message: a binding that does not copy
        /// the message hands this object to a queue, and the queue sets its own values.
        /// </summary>
        public void Restore(HorseMessage message)
        {
            message.Type = _type;
            message.ContentType = _contentType;
            message.WaitResponse = _waitResponse;

            if (!ReferenceEquals(message.Target, _target))
                message.SetTarget(_target);
        }
    }

    /// <summary>
    /// Pushes a message to router
    /// </summary>
    public virtual async Task<RouterPublishResult> Publish(MessagingClient sender, HorseMessage message)
    {
        try
        {
            if (!IsEnabled)
                return RouterPublishResult.Disabled;

            // A snapshot: bindings added or removed meanwhile do not change this publish
            Binding[] bindings = Bindings;
            if (bindings.Length == 0)
                return RouterPublishResult.NoBindings;

            MessageState state = new MessageState(message);

            RouterPublishResult result;
            switch (Method)
            {
                case RouteMethod.Distribute:
                    result = await Distribute(sender, message, bindings, state);
                    break;

                case RouteMethod.OnlyFirst:
                    result = await OnlyFirst(sender, message, bindings, state);
                    break;

                case RouteMethod.RoundRobin:
                    result = await RoundRobin(sender, message, bindings, state);
                    break;

                default:
                    return RouterPublishResult.Disabled;
            }

            if (result == RouterPublishResult.OkWillNotRespond || result == RouterPublishResult.OkAndWillBeRespond)
                Rider.Plugin.TriggerPluginHandlers(HorsePluginEvent.RouterPublish, Name, message);

            return result;
        }
        catch (Exception e)
        {
            Rider.SendError(HorseLogLevel.Error, HorseLogEvents.RouterPublish, $"Publish Router:{Name}", e);
            return RouterPublishResult.NoBindings;
        }
    }

    /// <summary>
    /// Sends the message to only first binding
    /// </summary>
    private static async Task<RouterPublishResult> OnlyFirst(MessagingClient sender, HorseMessage message, Binding[] bindings, MessageState state)
    {
        for (int i = 0; i < bindings.Length; i++)
        {
            if (i > 0)
                state.Restore(message);

            Binding binding = bindings[i];
            if (await binding.Send(sender, message))
                return binding.Interaction != BindingInteraction.None
                    ? RouterPublishResult.OkAndWillBeRespond
                    : RouterPublishResult.OkWillNotRespond;
        }

        return RouterPublishResult.NoReceivers;
    }

    /// <summary>
    /// Distributes the message to all bindings
    /// </summary>
    private static async Task<RouterPublishResult> Distribute(MessagingClient sender, HorseMessage message, Binding[] bindings, MessageState state)
    {
        RouterPublishResult result = RouterPublishResult.NoReceivers;
        for (int i = 0; i < bindings.Length; i++)
        {
            if (i > 0)
                state.Restore(message);

            Binding binding = bindings[i];
            bool sent = await binding.Send(sender, message);
            if (sent)
            {
                if (binding.Interaction != BindingInteraction.None)
                    result = RouterPublishResult.OkAndWillBeRespond;

                else if (result == RouterPublishResult.NoReceivers)
                    result = RouterPublishResult.OkWillNotRespond;
            }
        }

        return result;
    }

    /// <summary>
    /// Sends the message to only one binding within round robin algorithm
    /// </summary>
    private async Task<RouterPublishResult> RoundRobin(MessagingClient sender, HorseMessage message, Binding[] bindings, MessageState state)
    {
        uint len = (uint)bindings.Length;
        for (int i = 0; i < len; i++)
        {
            if (i > 0)
                state.Restore(message);

            // Lock free: the counter only grows (wrapping), the binding is chosen by its remainder
            uint next = unchecked((uint)Interlocked.Increment(ref _lastRoutedIndex));
            Binding binding = bindings[next % len];

            bool sent = await binding.Send(sender, message);
            if (sent)
                return binding.Interaction != BindingInteraction.None
                    ? RouterPublishResult.OkAndWillBeRespond
                    : RouterPublishResult.OkWillNotRespond;
        }

        return RouterPublishResult.NoReceivers;
    }

    #endregion
}
