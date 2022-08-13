using System;
using System.Collections.Generic;
using System.Data;
using EnumsNET;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Events;
using Horse.Messaging.Server.Containers;
using Horse.Messaging.Server.Events;
using Horse.Messaging.Server.Helpers;

namespace Horse.Messaging.Server.Routing
{
    /// <summary>
    /// Manages routers in messaging server
    /// </summary>
    public class RouterRider
    {
        private readonly ArrayContainer<IRouter> _routers = new ArrayContainer<IRouter>();

        /// <summary>
        /// Router message event handlers
        /// </summary>
        public ArrayContainer<IRouterMessageHandler> MessageHandlers { get; } = new ArrayContainer<IRouterMessageHandler>();

        /// <summary>
        /// All Queues of the server
        /// </summary>
        public IEnumerable<IRouter> Routers => _routers.All();

        /// <summary>
        /// Root horse rider object
        /// </summary>
        public HorseRider Rider { get; }

        /// <summary>
        /// Persistence configurator for routers.
        /// Settings this value to null disables the persistence for routers and they are lost after application restart.
        /// Default value is not null and saves queues into ./data/routers.json file
        /// </summary>
        public IPersistenceConfigurator<RouterConfiguration> PersistenceConfigurator { get; set; }

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
            CreateEvent = new EventManager(rider, HorseEventType.RouterCreate);
            RemoveEvent = new EventManager(rider, HorseEventType.RouterRemove);
            BindingAddEvent = new EventManager(rider, HorseEventType.RouterBindingAdd);
            BindingRemoveEvent = new EventManager(rider, HorseEventType.RouterBindingRemove);
            PersistenceConfigurator = new RouterPersistenceConfigurator("data", "routers.json");
        }

        internal void Initialize()
        {
            if (!KeepRouters) return;
            _initializing = true;

            if (PersistenceConfigurator != null)
            {
                RouterConfiguration[] configurations = PersistenceConfigurator.Load();
                foreach (RouterConfiguration config in configurations)
                {
                    IRouter router = CreateRouter(config);
                    if (router != null)
                        _routers.Add(router);
                }
            }

            _initializing = false;
        }

        /// <summary>
        /// Creates new Router and adds it to server routers.
        /// Throws exception if name is not eligible
        /// </summary>
        public IRouter Add(string name, RouteMethod method)
        {
            try
            {
                if (!Filter.CheckNameEligibility(name))
                    throw new NotSupportedException("Invalid router name");

                if (Rider.Options.RouterLimit > 0 && Rider.Options.RouterLimit >= _routers.Count())
                    throw new OperationCanceledException("Router limit is exceeded for the server");

                if (_routers.Find(x => x.Name == name) != null)
                    throw new DuplicateNameException();

                Router router = new Router(Rider, name, method);
                _routers.Add(router);

                CreateEvent.Trigger(name, new KeyValuePair<string, string>(HorseHeaders.ROUTE_METHOD, method.ToString()));

                if (PersistenceConfigurator != null)
                {
                    PersistenceConfigurator.Add(RouterConfiguration.Create(router));
                    PersistenceConfigurator.Save();
                }

                return router;
            }
            catch (Exception e)
            {
                Rider.SendError("ADD_ROUTER", e, $"RouterName:{name}");
                throw;
            }
        }

        /// <summary>
        /// Adds new router to server server routers
        /// Throws exception if name is not eligible
        /// </summary>
        public void Add(IRouter router)
        {
            try
            {
                if (!Filter.CheckNameEligibility(router.Name))
                    throw new InvalidOperationException("Invalid router name");

                if (Rider.Options.RouterLimit > 0 && Rider.Options.RouterLimit >= _routers.Count())
                    throw new OperationCanceledException("Router limit is exceeded for the server");

                if (_routers.Find(x => x.Name == router.Name) != null)
                    throw new DuplicateNameException();

                CreateEvent.Trigger(router.Name, new KeyValuePair<string, string>(HorseHeaders.ROUTE_METHOD, router.Method.ToString()));

                _routers.Add(router);

                if (PersistenceConfigurator != null)
                {
                    PersistenceConfigurator.Add(RouterConfiguration.Create(router));
                    PersistenceConfigurator.Save();
                }
            }
            catch (Exception e)
            {
                Rider.SendError("ADD_ROUTER", e, $"RouterName:{router?.Name}");
                throw;
            }
        }

        /// <summary>
        /// Removes the router from server routers
        /// </summary>
        public void Remove(IRouter router)
        {
            _routers.Remove(router);
            RemoveEvent.Trigger(router.Name);

            if (PersistenceConfigurator != null)
            {
                RouterConfiguration configuration = PersistenceConfigurator.Find(x => x.Name == router.Name);
                if (configuration != null)
                {
                    PersistenceConfigurator.Remove(configuration);
                    PersistenceConfigurator.Save();
                }
            }
        }

        /// <summary>
        /// Finds router by it's name
        /// </summary>
        public IRouter Find(string name)
        {
            return _routers.Find(x => x.Name == name);
        }

        private IRouter CreateRouter(RouterConfiguration configuration)
        {
            Router router = new Router(Rider, configuration.Name, Enums.Parse<RouteMethod>(configuration.Method, true, EnumFormat.Description));
            router.IsEnabled = configuration.IsEnabled;

            foreach (BindingConfiguration bd in configuration.Bindings)
            {
                Type type = Type.GetType(bd.Type);
                if (type == null)
                {
                    Rider.SendError("CreateRouter", new ArgumentException($"Type resolve error for binding {bd.Type}"), "Loading from Binding Definition");
                    continue;
                }

                Binding binding = (Binding) Activator.CreateInstance(type);
                binding.Name = bd.Name;
                binding.Target = bd.Target;
                binding.Priority = bd.Priority;
                binding.Interaction = Enums.Parse<BindingInteraction>(bd.Interaction, true, EnumFormat.Description);
                binding.RouteMethod = string.IsNullOrEmpty(bd.Method)
                    ? RouteMethod.Distribute
                    : Enums.Parse<RouteMethod>(bd.Method, true, EnumFormat.Description);

                router.AddBinding(binding);
            }

            return router;
        }
    }
}