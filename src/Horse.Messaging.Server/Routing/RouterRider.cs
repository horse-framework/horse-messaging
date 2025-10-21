using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using EnumsNET;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Events;
using Horse.Messaging.Server.Containers;
using Horse.Messaging.Server.Events;
using Horse.Messaging.Server.Helpers;
using Horse.Messaging.Server.Logging;

namespace Horse.Messaging.Server.Routing;

/// <summary>
/// Manages routers in messaging server
/// </summary>
public class RouterRider
{
    private readonly ConcurrentDictionary<string, Router> _routers = new ConcurrentDictionary<string, Router>();

    /// <summary>
    /// Router message event handlers
    /// </summary>
    public ArrayContainer<IRouterMessageHandler> MessageHandlers { get; } = new();

    /// <summary>
    /// All Queues of the server
    /// </summary>
    public IEnumerable<Router> Routers => _routers.Values;

    /// <summary>
    /// Root horse rider object
    /// </summary>
    public HorseRider Rider { get; }

    /// <summary>
    /// Cluster notifier for router operations
    /// </summary>
    internal RouterClusterNotifier ClusterNotifier { get; }

    /// <summary>
    /// Persistence configurator for routers.
    /// Settings this value to null disables the persistence for routers and they are lost after application restart.
    /// Default value is not null and saves routers into ./data/routers.json file
    /// </summary>
    public IOptionsConfigurator<RouterConfiguration> OptionsConfigurator { get; set; }

    /// <summary>
    /// Event Manage for HorseEventType.RouterCreate
    /// </summary>
    public EventManager CreateEvent { get; }

    /// <summary>
    /// Event Manage for HorseEventType.RouterRemove
    /// </summary>
    public EventManager RemoveEvent { get; }

    /// <summary>
    /// Event Manage for HorseEventType.RouterBindingAdd
    /// </summary>
    public EventManager BindingAddEvent { get; }

    /// <summary>
    /// Event Manage for HorseEventType.RouterBindingRemove
    /// </summary>
    public EventManager BindingRemoveEvent { get; }

    /// <summary>
    /// Save routers to local file
    /// </summary>
    public bool KeepRouters { get; set; } = true;

    private bool _initializing;

    /// <summary>
    /// Creates new queue rider
    /// </summary>
    internal RouterRider(HorseRider rider)
    {
        Rider = rider;
        ClusterNotifier = new RouterClusterNotifier(this, Rider.Cluster);
        CreateEvent = new EventManager(rider, HorseEventType.RouterCreate);
        RemoveEvent = new EventManager(rider, HorseEventType.RouterRemove);
        BindingAddEvent = new EventManager(rider, HorseEventType.RouterBindingAdd);
        BindingRemoveEvent = new EventManager(rider, HorseEventType.RouterBindingRemove);
        OptionsConfigurator = new RouterOptionsConfigurator(rider, "routers.json");
    }

    internal void Initialize()
    {
        if (!KeepRouters) return;
        _initializing = true;

        if (OptionsConfigurator != null)
        {
            RouterConfiguration[] configurations = OptionsConfigurator.Load();
            foreach (RouterConfiguration config in configurations)
            {
                Router router = CreateRouter(config);
                if (router != null)
                    _routers.TryAdd(router.Name, router);
            }
        }

        _initializing = false;
    }

    /// <summary>
    /// Creates new Router and adds it to server routers.
    /// Throws exception if name is not eligible
    /// </summary>
    public Router Add(string name, RouteMethod method)
    {
        return Add(name, method, true);
    }

    /// <summary>
    /// Creates new Router and adds it to server routers.
    /// Throws exception if name is not eligible
    /// </summary>
    internal Router Add(string name, RouteMethod method, bool notifyCluster)
    {
        try
        {
            if (!Filter.CheckNameEligibility(name))
                throw new NotSupportedException("Invalid router name");

            if (Rider.Options.RouterLimit > 0 && Rider.Options.RouterLimit >= _routers.Count)
                throw new OperationCanceledException("Router limit is exceeded for the server");

            if (_routers.ContainsKey(name))
                throw new DuplicateNameException();

            Router router = new Router(Rider, name, method);
            _routers.TryAdd(router.Name, router);

            CreateEvent.Trigger(name, new KeyValuePair<string, string>(HorseHeaders.ROUTE_METHOD, method.ToString()));

            if (OptionsConfigurator != null)
            {
                OptionsConfigurator.Add(RouterConfiguration.Create(router));
                OptionsConfigurator.Save();
            }

            if (notifyCluster)
                ClusterNotifier.SendRouterCreated(router);

            return router;
        }
        catch (Exception e)
        {
            Rider.SendError(HorseLogLevel.Error, HorseLogEvents.RouterAdd, $"Add Router: {name}", e);
            throw;
        }
    }

    /// <summary>
    /// Adds new router to server server routers
    /// Throws exception if name is not eligible
    /// </summary>
    public Router Add(Router router)
    {
        return Add(router, true);
    }

    /// <summary>
    /// Adds new router to server server routers
    /// Throws exception if name is not eligible
    /// </summary>
    internal Router Add(Router router, bool notifyCluster)
    {
        try
        {
            if (!Filter.CheckNameEligibility(router.Name))
                throw new InvalidOperationException("Invalid router name");

            if (Rider.Options.RouterLimit > 0 && Rider.Options.RouterLimit >= _routers.Count)
                throw new OperationCanceledException("Router limit is exceeded for the server");

            if (_routers.ContainsKey(router.Name))
                throw new DuplicateNameException();

            CreateEvent.Trigger(router.Name, new KeyValuePair<string, string>(HorseHeaders.ROUTE_METHOD, router.Method.ToString()));

            _routers.TryAdd(router.Name, router);

            if (OptionsConfigurator != null)
            {
                OptionsConfigurator.Add(RouterConfiguration.Create(router));
                OptionsConfigurator.Save();
            }

            if (notifyCluster)
                ClusterNotifier.SendRouterCreated(router);

            return router;
        }
        catch (Exception e)
        {
            Rider.SendError(HorseLogLevel.Error, HorseLogEvents.RouterAdd, $"Add Router: {router?.Name}", e);
            throw;
        }
    }

    /// <summary>
    /// Removes the router from server routers
    /// </summary>
    public void Remove(Router router)
    {
        Remove(router, true);
    }

    /// <summary>
    /// Removes the router from server routers
    /// </summary>
    internal void Remove(Router router, bool notifyCluster)
    {
        _routers.TryRemove(router.Name, out _);
        RemoveEvent.Trigger(router.Name);

        if (OptionsConfigurator != null)
        {
            RouterConfiguration configuration = OptionsConfigurator.Find(x => x.Name == router.Name);
            if (configuration != null)
            {
                OptionsConfigurator.Remove(configuration);
                OptionsConfigurator.Save();
            }
        }

        if (notifyCluster)
            ClusterNotifier.SendRouterRemoved(router);
    }

    /// <summary>
    /// Finds router by it's name
    /// </summary>
    public Router Find(string name)
    {
        _routers.TryGetValue(name, out Router router);
        return router;
    }

    internal Router CreateRouter(RouterConfiguration configuration)
    {
        Router router = new Router(Rider, configuration.Name, Enums.Parse<RouteMethod>(configuration.Method, true, EnumFormat.Description));
        router.IsEnabled = configuration.IsEnabled;

        foreach (BindingConfiguration bd in configuration.Bindings)
        {
            Binding binding = CreateBinding(bd);

            if (binding != null)
                router.AddBinding(binding);
        }

        return router;
    }

    internal Binding CreateBinding(BindingConfiguration configuration)
    {
        Type type = Type.GetType(configuration.Type);
        if (type == null)
        {
            Rider.SendError(HorseLogLevel.Error, HorseLogEvents.RouterCreate, $"Type resolve error for binding {configuration.Type}, Loading from Binding Definition", null);
            return null;
        }

        Binding binding = (Binding)Activator.CreateInstance(type);
        binding.Name = configuration.Name;
        binding.Target = configuration.Target;
        binding.Priority = configuration.Priority;
        binding.Interaction = Enums.Parse<BindingInteraction>(configuration.Interaction, true, EnumFormat.Description);
        binding.RouteMethod = string.IsNullOrEmpty(configuration.Method)
            ? RouteMethod.Distribute
            : Enums.Parse<RouteMethod>(configuration.Method, true, EnumFormat.Description);

        return binding;
    }
}