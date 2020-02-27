using System;
using System.Threading.Tasks;
using Twino.Ioc;
using Twino.Protocols.Http;

namespace Twino.Mvc.Middlewares
{
    /// <summary>
    /// Middleware runner object.
    /// Created for per request organizes and executes sequence all middlewares for the request
    /// </summary>
    internal class MiddlewareRunner
    {
        /// <summary>
        /// Last middleware's result object
        /// </summary>
        internal IActionResult LastResult { get; private set; }

        private readonly TwinoMvc _mvc;
        private readonly IContainerScope _scope;

        public MiddlewareRunner(TwinoMvc mvc, IContainerScope scope)
        {
            _mvc = mvc;
            _scope = scope;
        }

        private void SetResult(IActionResult result = null)
        {
            LastResult = result;
        }

        /// <summary>
        /// Executes all middlewares
        /// </summary>
        internal async Task RunSequence(MvcAppBuilder app, HttpRequest request, HttpResponse response)
        {
            foreach (MiddlewareDescriptor descriptor in app.Descriptors)
            {
                IMiddleware middleware = await CreateInstance(descriptor);
                await middleware.Invoke(request, response, SetResult);

                if (LastResult != null)
                    break;
            }
        }

        /// <summary>
        /// Creates middleware object from descriptor
        /// </summary>
        private async Task<IMiddleware> CreateInstance(MiddlewareDescriptor descriptor)
        {
            if (descriptor.Instance != null)
                return descriptor.Instance;

            if (descriptor.ConstructorParameters.Length == 0)
                return (IMiddleware)Activator.CreateInstance(descriptor.MiddlewareType);

            object[] parameters = new object[descriptor.ConstructorParameters.Length];
            for (int i = 0; i < descriptor.ConstructorParameters.Length; i++)
            {
                Type type = descriptor.ConstructorParameters[i];
                object o = await _mvc.Services.Get(type, _scope);
                parameters[i] = o;
            }

            return (IMiddleware)Activator.CreateInstance(descriptor.MiddlewareType, parameters);
        }
    }
}