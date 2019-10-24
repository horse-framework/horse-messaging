using Twino.Mvc.Auth;
using Twino.Mvc.Controllers;
using Twino.Mvc.Errors;
using Twino.Mvc.Middlewares;
using Twino.Mvc.Results;
using Twino.Mvc.Routing;
using Twino.Mvc.Services;
using Twino.Server;
using Twino.Server.Http;
using Twino.Server.WebSockets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Twino.Mvc
{
    /// <summary>
    /// Twino Facade object of Twino HTTP Server
    /// </summary>
    public class TwinoMvc : IDisposable
    {
        #region Properties

        /// <summary>
        /// All routes. This list is prepared with Init method.
        /// Loads all types in assembly implemented from IController and their actions with Http Method Attribute.
        /// </summary>
        public List<Route> Routes { get; private set; }

        /// <summary>
        /// HTTP server of Twino MVC
        /// </summary>
        public TwinoServer Server { get; }

        /// <summary>
        /// Twino MVC Dependency Injection service container
        /// </summary>
        public IServiceContainer Services { get; private set; }

        /// <summary>
        /// Twino MVC Route finder. For every HTTP Request, this finder matches the request Path with Routes list.
        /// </summary>
        public IRouteFinder RouteFinder { get; set; }

        /// <summary>
        /// Twino MVC Controller Factory. For every Request, when the controller if found for the requested path,
        /// ControllerFactory creates the controller object and fills all properties and Dependency Injection parameters.
        /// </summary>
        public IControllerFactory ControllerFactory { get; set; }

        /// <summary>
        /// Reads User information from the request and creates ClaimsPrincipal class
        /// </summary>
        public IClaimsPrincipalValidator ClaimsPrincipalValidator { get; set; }

        /// <summary>
        /// Pre-defined Policy container
        /// </summary>
        public IPolicyContainer Policies { get; set; }

        /// <summary>
        /// Non-Development Mode Error Handler object
        /// </summary>
        public IErrorHandler ErrorHandler { get; set; }

        /// <summary>
        /// Used for 404 Results. As default, its 404 StatusCodeResult.
        /// In order to customize, development can change this property.
        /// </summary>
        public IActionResult NotFoundResult { get; set; }

        /// <summary>
        /// Development mode for the Twino Server.
        /// It's loaded from ServerOptions (usually from twino.json file)
        /// Can be changed progammatically
        /// </summary>
        public bool IsDevelopment { get; set; }

        /// <summary>
        /// Handler action for middlewares.
        /// Called for each request before the MVC Request processing starts.
        /// </summary>
        internal Action<IMvcApp> RunnerAction { get; set; }

        #endregion

        #region Constructors - Destructors

        /// <summary>
        /// Creates default MVC HTTP Server without WebSocket support.
        /// </summary>
        public TwinoMvc() : this(default(IClientFactory), null, null)
        {
        }

        /// <summary>
        /// Creates Default MVC HTTP Server with WebSocket support.
        /// </summary>
        public TwinoMvc(ClientFactoryHandler clientHandler)
            : this(new DefaultClientFactory(clientHandler), null, null)
        {
        }

        /// <summary>
        /// Creates Default MVC HTTP Server with WebSocket support.
        /// </summary>
        public TwinoMvc(IClientFactory clientFactory)
            : this(clientFactory, null, null)
        {
        }

        /// <summary>
        /// Creates Default HTTP Server without WebSocket support.
        /// Server options can be set programmatically.
        /// </summary>
        public TwinoMvc(ServerOptions options)
            : this(default(IClientFactory), null, options)
        {
        }

        /// <summary>
        /// Creates Default MVC HTTP Server with WebSocket support.
        /// Server options can be set programmatically.
        /// </summary>
        public TwinoMvc(ClientFactoryHandler clientHandler, ServerOptions options)
            : this(new DefaultClientFactory(clientHandler), null, options)
        {
        }

        /// <summary>
        /// Creates Default MVC HTTP Server with WebSocket support.
        /// Server options can be set programmatically.
        /// </summary>
        public TwinoMvc(IClientFactory clientFactory, ServerOptions options)
            : this(clientFactory, null, options)
        {
        }

        /// <summary>
        /// Creates customized HTTP and WebSocket server
        /// </summary>
        public TwinoMvc(ClientFactoryHandler clientHandler, IClientContainer clientContainer)
            : this(new DefaultClientFactory(clientHandler), clientContainer, null)
        {
        }

        /// <summary>
        /// Creates customized HTTP and WebSocket server
        /// </summary>
        public TwinoMvc(IClientFactory clientFactory, IClientContainer clientContainer)
            : this(clientFactory, clientContainer, null)
        {
        }

        /// <summary>
        /// Creates customized HTTP and WebSocket server
        /// Server options can be set programmatically.
        /// </summary>
        public TwinoMvc(ClientFactoryHandler clientHandler, IClientContainer clientContainer, ServerOptions options)
            : this(new DefaultClientFactory(clientHandler), clientContainer, options)
        {
        }

        /// <summary>
        /// Creates customized HTTP and WebSocket server
        /// Server options can be set programmatically.
        /// </summary>
        public TwinoMvc(IClientFactory clientFactory, IClientContainer clientContainer, ServerOptions options)
        {
            Routes = new List<Route>();
            Services = new ServiceContainer();
            RouteFinder = new RouteFinder();
            ControllerFactory = new ControllerFactory();
            NotFoundResult = StatusCodeResult.NotFound();
            ErrorHandler = new DefaultErrorHandler();
            Policies = new PolicyContainer();

            IHttpRequestHandler requestHandler = new MvcRequestHandler(this);

            Server = options == null
                         ? new TwinoServer(requestHandler, clientFactory, clientContainer)
                         : new TwinoServer(requestHandler, clientFactory, clientContainer, options);
        }

        /// <summary>
        /// Disposes Twino MVC and stops the HTTP Server
        /// </summary>
        public void Dispose()
        {
            Services = new ServiceContainer();
            Server.Stop();
        }

        #endregion

        #region Init

        /// <summary>
        /// Inits Twino MVC
        /// </summary>
        public void Init(Action<TwinoMvc> action)
        {
            Init();
            action(this);
        }

        /// <summary>
        /// Inits Twino MVC
        /// </summary>
        public void Init()
        {
            Routes = new List<Route>();
            CreateRoutes();
        }

        /// <summary>
        /// Loads all IController types from the assembly and searches route info in all types.
        /// </summary>
        public void CreateRoutes(Assembly assembly = null)
        {
            RouteBuilder builder = new RouteBuilder();

            Type interfaceType = typeof(IController);

            List<Assembly> assemblies = new List<Assembly>();
            if (assembly == null)
            {
                Assembly entryAssembly = Assembly.GetEntryAssembly();
                if (entryAssembly == null)
                    throw new ArgumentNullException("Entry Assembly could not be found");

                assemblies.Add(entryAssembly);
                assemblies.AddRange(entryAssembly.GetReferencedAssemblies().Select(Assembly.Load));
            }
            else
                assemblies.Add(assembly);

            List<Type> types = assemblies
                               .SelectMany(x => x.GetTypes())
                               .Where(type => interfaceType.IsAssignableFrom(type))
                               .ToList();

            foreach (Type type in types)
            {
                if (type.IsInterface)
                    continue;

                if (type.IsAssignableFrom(typeof(TwinoController)) && typeof(TwinoController).IsAssignableFrom(type))
                    continue;

                IEnumerable<Route> routes = builder.BuildRoutes(type);
                foreach (Route route in routes)
                    Routes.Add(route);
            }
        }

        #endregion

        #region Run

        /// <summary>
        /// Runs Twino MVC Server as sync, without middleware implementation
        /// </summary>
        public void Run()
        {
            Run(null, false);
        }

        /// <summary>
        /// Runs Twino MVC Server as sync, with middleware implementation
        /// </summary>
        public void Run(Action<IMvcApp> runner)
        {
            Run(runner, false);
        }

        /// <summary>
        /// Runs Twino MVC Server as async, without middleware implementation
        /// </summary>
        public void RunAsync()
        {
            Run(null, true);
        }

        /// <summary>
        /// Runs Twino MVC Server as async, with middleware implementation
        /// </summary>
        public void RunAsync(Action<IMvcApp> runner)
        {
            Run(runner, true);
        }

        /// <summary>
        /// Runs Twino MVC Server
        /// </summary>
        private void Run(Action<IMvcApp> runner, bool async)
        {
            RunnerAction = runner;
            Server.Start();

            if (!async)
                Server.BlockWhileRunning();
        }

        #endregion
    }
}