using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
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
    /// Serializes changes of the saved configurations.
    /// Replacing the configuration of a router is a remove, an add and a save; two of them running
    /// together for the same router could leave two entries for it.
    /// </summary>
    private readonly object _saveLock = new();

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

    /// <summary>
    /// Loads the saved routers.
    /// The file is not written while loading, except to remove duplicate entries of a router.
    /// </summary>
    internal void Initialize()
    {
        if (!KeepRouters || OptionsConfigurator == null)
            return;

        RouterConfiguration[] configurations = OptionsConfigurator.Load();

        // Older versions could save a router more than once (a replica saved it while creating it and
        // again when adding it). Each later change appended the newest state, so the last entry is the current one.
        Dictionary<string, RouterConfiguration> latest = new Dictionary<string, RouterConfiguration>();
        HashSet<string> duplicates = null;

        foreach (RouterConfiguration configuration in configurations)
        {
            if (configuration?.Name == null)
                continue;

            if (latest.ContainsKey(configuration.Name))
                (duplicates ??= new HashSet<string>()).Add(configuration.Name);

            latest[configuration.Name] = configuration;
        }

        foreach (RouterConfiguration configuration in latest.Values)
        {
            Router router = CreateRouter(configuration);
            if (router != null)
                _routers.TryAdd(router.Name, router);
        }

        if (duplicates == null)
            return;

        lock (_saveLock)
        {
            try
            {
                foreach (string name in duplicates)
                {
                    RemoveConfigurations(OptionsConfigurator, name);
                    OptionsConfigurator.Add(latest[name]);
                }

                OptionsConfigurator.Save();
            }
            catch (Exception e)
            {
                Rider.SendError(HorseLogLevel.Error, HorseLogEvents.RouterSave, "Save Routers: removing duplicate entries", e);
            }
        }
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

            if (Rider.Options.RouterLimit > 0 && _routers.Count >= Rider.Options.RouterLimit)
                throw new OperationCanceledException("Router limit is exceeded for the server");

            // TryAdd decides: two requests creating the same router passed a ContainsKey check together,
            // and the second one got a router object that was not on the server
            Router router = new Router(Rider, name, method);
            if (!_routers.TryAdd(router.Name, router))
                throw new DuplicateNameException();

            CreateEvent.Trigger(name, new KeyValuePair<string, string>(HorseHeaders.ROUTE_METHOD, method.ToString()));
            SaveRouterConfiguration(router);

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

            if (Rider.Options.RouterLimit > 0 && _routers.Count >= Rider.Options.RouterLimit)
                throw new OperationCanceledException("Router limit is exceeded for the server");

            if (!_routers.TryAdd(router.Name, router))
                throw new DuplicateNameException();

            CreateEvent.Trigger(router.Name, new KeyValuePair<string, string>(HorseHeaders.ROUTE_METHOD, router.Method.ToString()));
            SaveRouterConfiguration(router);

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
        // Removes only this router object. A repeated removal through an old reference must not
        // remove (and delete the saved configuration of) a newer router added with the same name.
        if (!_routers.TryRemove(new KeyValuePair<string, Router>(router.Name, router)))
            return;

        RemoveEvent.Trigger(router.Name);
        router.PublishEvent.Dispose();
        DeleteRouterConfiguration(router.Name);

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

    #region Persistence

    /// <summary>
    /// Replaces the saved configuration of the router with its current state and saves it.
    /// A failed save is logged and does not undo the change: the router, its bindings and the cluster
    /// stay as they are, and the configurator keeps the new state, so the next successful save writes it.
    /// </summary>
    internal void SaveRouterConfiguration(Router router)
    {
        IOptionsConfigurator<RouterConfiguration> options = OptionsConfigurator;
        if (options == null)
            return;

        lock (_saveLock)
        {
            try
            {
                RemoveConfigurations(options, router.Name);
                options.Add(RouterConfiguration.Create(router));
                options.Save();
            }
            catch (Exception e)
            {
                Rider.SendError(HorseLogLevel.Error, HorseLogEvents.RouterSave, $"Save Router: {router.Name}", e);
            }
        }
    }

    /// <summary>
    /// Removes the saved configuration of the router
    /// </summary>
    private void DeleteRouterConfiguration(string name)
    {
        IOptionsConfigurator<RouterConfiguration> options = OptionsConfigurator;
        if (options == null)
            return;

        lock (_saveLock)
        {
            try
            {
                if (RemoveConfigurations(options, name))
                    options.Save();
            }
            catch (Exception e)
            {
                Rider.SendError(HorseLogLevel.Error, HorseLogEvents.RouterSave, $"Remove Router: {name}", e);
            }
        }
    }

    /// <summary>
    /// Removes every saved entry of the router. Older versions could save a router twice,
    /// and a router whose second entry stayed came back after a restart.
    /// Returns true if an entry is removed.
    /// </summary>
    private static bool RemoveConfigurations(IOptionsConfigurator<RouterConfiguration> options, string name)
    {
        bool removed = false;
        RouterConfiguration configuration = options.Find(x => x.Name == name);

        while (configuration != null)
        {
            options.Remove(configuration);
            removed = true;

            RouterConfiguration next = options.Find(x => x.Name == name);

            // a custom configurator that did not remove it would loop forever
            if (ReferenceEquals(next, configuration))
                break;

            configuration = next;
        }

        return removed;
    }

    /// <summary>
    /// Creates a router from its saved configuration (startup or a create message of another node).
    /// The bindings are added without events, saves or cluster messages: the router is not added to the server yet.
    /// A binding whose type cannot be created is kept in the router's configuration, so later saves do not delete it.
    /// </summary>
    internal Router CreateRouter(RouterConfiguration configuration)
    {
        Router router = new Router(Rider, configuration.Name, Enums.Parse<RouteMethod>(configuration.Method, true, EnumFormat.Description));
        router.IsEnabled = configuration.IsEnabled;

        if (configuration.Bindings != null)
        {
            foreach (BindingConfiguration bd in configuration.Bindings)
            {
                Binding binding = CreateBinding(bd);

                if (binding != null)
                    router.LoadBinding(binding);
                else
                    router.KeepUnresolvedBinding(bd);
            }
        }

        return router;
    }

    /// <summary>
    /// Creates a binding from its saved configuration. Returns null if the binding cannot be created.
    /// </summary>
    internal Binding CreateBinding(BindingConfiguration configuration)
    {
        Type type = ResolveBindingType(configuration);
        if (type == null)
        {
            Rider.SendError(HorseLogLevel.Error, HorseLogEvents.RouterCreate, $"Type resolve error for binding {configuration.Type}, Loading from Binding Definition", null);
            return null;
        }

        try
        {
            Binding binding = (Binding)Activator.CreateInstance(type);
            binding.Name = configuration.Name;
            binding.Target = configuration.Target;
            binding.ContentType = configuration.ContentType;
            binding.Priority = configuration.Priority;
            binding.Interaction = string.IsNullOrEmpty(configuration.Interaction)
                ? BindingInteraction.None
                : Enums.Parse<BindingInteraction>(configuration.Interaction, true, EnumFormat.Description);
            binding.RouteMethod = string.IsNullOrEmpty(configuration.Method)
                ? RouteMethod.Distribute
                : Enums.Parse<RouteMethod>(configuration.Method, true, EnumFormat.Description);

            return binding;
        }
        catch (Exception e)
        {
            Rider.SendError(HorseLogLevel.Error, HorseLogEvents.RouterCreate, $"Create binding {configuration.Name} of type {configuration.Type}", e);
            return null;
        }
    }

    /// <summary>
    /// Finds the type of a saved binding.
    /// Configurations saved by older versions (or sent by older nodes) have the type name without its
    /// assembly, which Type.GetType finds only in this assembly, so custom binding types are searched in
    /// the loaded assemblies too. Only Binding types are returned: configurations come from files and other nodes.
    /// </summary>
    private static Type ResolveBindingType(BindingConfiguration configuration)
    {
        return ResolveBindingType(configuration.Type, configuration.AssemblyName);
    }

    /// <summary>
    /// Finds a binding type by its full name (or assembly qualified name) and optional assembly name.
    /// Returns null if the type is not found, is abstract or is not a Binding.
    /// </summary>
    internal static Type ResolveBindingType(string typeName, string assemblyName)
    {
        if (string.IsNullOrEmpty(typeName))
            return null;

        Type type = null;

        if (!string.IsNullOrEmpty(assemblyName))
            type = FindType($"{typeName}, {assemblyName}");

        type ??= FindType(typeName);

        if (type == null)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    type = assembly.GetType(typeName, false);
                }
                catch
                {
                    type = null;
                }

                if (type != null)
                    break;
            }
        }

        if (type == null || type.IsAbstract || !typeof(Binding).IsAssignableFrom(type))
            return null;

        return type;
    }

    private static Type FindType(string name)
    {
        try
        {
            return Type.GetType(name, false);
        }
        catch
        {
            // the assembly of the type cannot be loaded
            return null;
        }
    }

    #endregion
}
