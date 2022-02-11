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

        /// <summary>
        /// Configuration filename for saved routers
        /// </summary>
        internal string RouterConfigurationFilename { get; set; }

        /// <summary>
        /// Returns true if persistent routers are activated
        /// </summary>
        public bool PersistentRouters { get; internal set; }

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
            // TODO : Memo
            return;
            if (!PersistentRouters || string.IsNullOrEmpty(RouterConfigurationFilename))
                return;

            if (!System.IO.File.Exists(RouterConfigurationFilename))
                return;

            string json = System.IO.File.ReadAllText(RouterConfigurationFilename);
            RouterDefinition[] definitions = System.Text.Json.JsonSerializer.Deserialize<RouterDefinition[]>(json);

            foreach (RouterDefinition definition in definitions)
            {
                IRouter router = CreateRouter(definition);
                if (router != null)
                    _routers.Add(router);
            }
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
                    throw new InvalidOperationException("Invalid router name");

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

        private IRouter CreateRouter(RouterDefinition definition)
        {
            Router router = new Router(Rider, definition.Name, definition.Method);
            router.IsEnabled = definition.IsEnabled;

            foreach (BindingDefinition bd in definition.Bindings)
            {
                Binding binding = null;

                switch (bd.Type.ToLower())
                {
                    case "direct":
                        binding = new DirectBinding(bd.Name, bd.Target, bd.Priority, bd.Interaction, bd.Method ?? RouteMethod.Distribute);
                        break;

                    case "queue":
                        binding = new QueueBinding(bd.Name, bd.Target, bd.Priority, bd.Interaction);
                        break;

                    case "topic":
                        binding = new TopicBinding(bd.Name, bd.Target, bd.ContentType, bd.Priority, bd.Interaction, bd.Method ?? RouteMethod.Distribute);
                        break;

                    case "http":
                        HttpBindingMethod httpBindingMethod = HttpBindingMethod.Get;
                        if (bd.ContentType.HasValue)
                            httpBindingMethod = (HttpBindingMethod) Convert.ToInt32(bd.ContentType.HasValue);

                        binding = new HttpBinding(bd.Name, bd.Target, httpBindingMethod, bd.Priority, bd.Interaction);
                        break;

                    case "dynamic":
                        binding = new DynamicQueueBinding(bd.Name, bd.Priority, bd.Interaction);
                        break;
                }

                if (binding != null)
                    router.AddBinding(binding);
            }

            return router;
        }

        internal void SaveRouters()
        {
            // TODO : Memo
            return;
            List<RouterDefinition> definitions = new List<RouterDefinition>();

            foreach (IRouter router in _routers.All())
            {
                RouterDefinition definition = new RouterDefinition
                {
                    Name = router.Name,
                    Method = router.Method,
                    IsEnabled = router.IsEnabled,
                    Bindings = new List<BindingDefinition>()
                };

                foreach (Binding binding in router.GetBindings())
                {
                    BindingDefinition bindingDefinition = new BindingDefinition
                    {
                        Name = binding.Name,
                        Interaction = binding.Interaction,
                        Target = binding.Target,
                        ContentType = binding.ContentType,
                        Priority = binding.Priority
                    };

                    if (binding is QueueBinding)
                    {
                        bindingDefinition.Type = "queue";
                    }
                    else if (binding is TopicBinding topicBinding)
                    {
                        bindingDefinition.Type = "topic";
                        bindingDefinition.Method = topicBinding.RouteMethod;
                    }
                    else if (binding is DirectBinding directBinding)
                    {
                        bindingDefinition.Type = "direct";
                        bindingDefinition.Method = directBinding.RouteMethod;
                    }
                    else if (binding is DynamicQueueBinding dynamicQueueBinding)
                    {
                        bindingDefinition.Type = "dynamic";
                        
                    }
                    else if (binding is HttpBinding httpBinding)
                    {
                        bindingDefinition.Type = "http";
                    }
                    
                    definition.Bindings.Add(bindingDefinition);
                }
                
                definitions.Add(definition);
            }

            try
            {
                string json = System.Text.Json.JsonSerializer.Serialize(definitions);
                System.IO.File.WriteAllText(RouterConfigurationFilename, json);
            }
            catch (Exception e)
            {
                Rider.SendError("SaveRouters", e, null);
            }
        }
    }
}