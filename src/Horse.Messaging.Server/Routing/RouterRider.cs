using System;
using System.Collections.Generic;
using System.Data;
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
        }

        internal void Initialize()
        {
            string fullpath = $"{Rider.DataPath}/routers.json";
            if (!System.IO.File.Exists(fullpath))
            {
                System.IO.File.WriteAllText(fullpath, "[]");
                return;
            }

            _initializing = true;
            string json = System.IO.File.ReadAllText(fullpath);
            RouterConfigData[] definitions = System.Text.Json.JsonSerializer.Deserialize<RouterConfigData[]>(json);

            foreach (RouterConfigData definition in definitions)
            {
                IRouter router = CreateRouter(definition);
                if (router != null)
                    _routers.Add(router);
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
                SaveRouters();

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
                SaveRouters();
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
            SaveRouters();
        }

        /// <summary>
        /// Finds router by it's name
        /// </summary>
        public IRouter Find(string name)
        {
            return _routers.Find(x => x.Name == name);
        }

        private IRouter CreateRouter(RouterConfigData configData)
        {
            Router router = new Router(Rider, configData.Name, configData.Method);
            router.IsEnabled = configData.IsEnabled;

            foreach (BindingConfigData bd in configData.Bindings)
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
                binding.Interaction = bd.Interaction;
                binding.RouteMethod = bd.Method ?? RouteMethod.Distribute;

                router.AddBinding(binding);
            }

            return router;
        }

        internal void SaveRouters()
        {
            if (_initializing)
                return;

            List<RouterConfigData> definitions = new List<RouterConfigData>();

            foreach (IRouter router in _routers.All())
            {
                RouterConfigData configData = new RouterConfigData
                {
                    Name = router.Name,
                    Method = router.Method,
                    IsEnabled = router.IsEnabled,
                    Bindings = new List<BindingConfigData>()
                };

                foreach (Binding binding in router.GetBindings())
                {
                    BindingConfigData bindingConfigData = new BindingConfigData
                    {
                        Name = binding.Name,
                        Interaction = binding.Interaction,
                        Target = binding.Target,
                        ContentType = binding.ContentType,
                        Priority = binding.Priority,
                        Type = binding.GetType().FullName,
                        Method = binding.RouteMethod
                    };

                    configData.Bindings.Add(bindingConfigData);
                }

                definitions.Add(configData);
            }

            try
            {
                string json = System.Text.Json.JsonSerializer.Serialize(definitions.ToArray());
                System.IO.File.WriteAllText($"{Rider.DataPath}/routers.json", json);
            }
            catch (Exception e)
            {
                Rider.SendError("SaveRouters", e, null);
            }
        }
    }
}