using Twino.Mvc.Auth;
using Twino.Mvc.Controllers;
using Twino.Mvc.Errors;
using Twino.Mvc.Middlewares;
using Twino.Mvc.Results;
using Twino.Mvc.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using Twino.Core;
using Twino.Ioc;

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
        /// File download routes
        /// </summary>
        public List<FileRoute> FileRoutes { get; private set; }

        /// <summary>
        /// Server objects of the MVC
        /// </summary>
        public ITwinoServer Server { get; set; }

        /// <summary>
        /// Default HTTP status results
        /// </summary>
        public Dictionary<HttpStatusCode, IActionResult> StatusCodeResults { get; } = new Dictionary<HttpStatusCode, IActionResult>();

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
        /// Mvc application builder
        /// </summary>
        internal MvcAppBuilder AppBuilder { get; private set; }

        #endregion

        #region Constructors - Destructors

        public TwinoMvc()
        {
            Routes = new List<Route>();
            Services = new ServiceContainer();
            RouteFinder = new RouteFinder();
            ControllerFactory = new ControllerFactory();
            NotFoundResult = StatusCodeResult.NotFound();
            ErrorHandler = new DefaultErrorHandler();
            Policies = new PolicyContainer();

            AppBuilder = new MvcAppBuilder(this);
        }

        /// <summary>
        /// Disposes Twino MVC and stops the HTTP Server
        /// </summary>
        public void Dispose()
        {
            Services = new ServiceContainer();
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
            FileRoutes = new List<FileRoute>();
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

        /// <summary>
        /// Loads all IController types from the assembly
        /// </summary>
        public void ImportAssembly(Assembly assembly)
        {
            CreateRoutes(assembly);
        }

        #endregion

        #region Run

        /// <summary>
        /// Runs Twino MVC Server as async, with middleware implementation
        /// </summary>
        public void Use(Action<IMvcAppBuilder> action)
        {
            if (action != null)
                action(AppBuilder);
        }

        #endregion
    }
}