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
        public EventManager RouterCreateEvent { get; }

        /// <summary>
        /// Event Manage for HorseEventType.RouterRemove
        /// </summary>
        public EventManager RouterRemoveEvent { get; }

        /// <summary>
        /// Event Manage for HorseEventType.RouterBindingAdd
        /// </summary>
        public EventManager RouterBindingAddEvent { get; }

        /// <summary>
        /// Event Manage for HorseEventType.RouterBindingRemove
        /// </summary>
        public EventManager RouterBindingRemoveEvent { get; }

        /// <summary>
        /// Creates new queue rider
        /// </summary>
        internal RouterRider(HorseRider rider)
        {
            Rider = rider;
            RouterCreateEvent = new EventManager(rider, HorseEventType.RouterCreate);
            RouterRemoveEvent = new EventManager(rider, HorseEventType.RouterRemove);
            RouterBindingAddEvent = new EventManager(rider, HorseEventType.RouterBindingAdd);
            RouterBindingRemoveEvent = new EventManager(rider, HorseEventType.RouterBindingRemove);
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

                RouterCreateEvent.Trigger(name, new KeyValuePair<string, string>(HorseHeaders.ROUTE_METHOD, method.ToString()));

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

                RouterCreateEvent.Trigger(router.Name, new KeyValuePair<string, string>(HorseHeaders.ROUTE_METHOD, router.Method.ToString()));

                _routers.Add(router);
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
            RouterRemoveEvent.Trigger(router.Name);
        }

        /// <summary>
        /// Finds router by it's name
        /// </summary>
        public IRouter Find(string name)
        {
            return _routers.Find(x => x.Name == name);
        }
    }
}