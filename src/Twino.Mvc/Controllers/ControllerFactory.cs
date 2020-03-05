using System;
using System.Reflection;
using System.Threading.Tasks;
using Twino.Ioc;
using Twino.Protocols.Http;

namespace Twino.Mvc.Controllers
{
    /// <summary>
    /// Factory object for the controller.
    /// For each request, this objects creates a controller from the specified parameters
    /// </summary>
    public class ControllerFactory : IControllerFactory
    {
        /// <summary>
        /// Creates new instance of a TwinoController object
        /// </summary>
        public async Task<TwinoController> CreateInstance(TwinoMvc mvc, Type controllerType, HttpRequest request, HttpResponse response, IContainerScope scope)
        {
            ConstructorInfo[] constructors = controllerType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

            if (constructors.Length == 0)
                throw new InvalidOperationException("There is no accessible constructor found in " + controllerType.FullName);
            else if (constructors.Length > 1)
                throw new InvalidOperationException($"{controllerType.FullName} must have only one accessible constructor.");

            ConstructorInfo constructor = constructors[0];

            ParameterInfo[] parameters = constructor.GetParameters();
            object[] values = new object[parameters.Length];

            //each parameter must be provided from the service container
            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo p = parameters[i];
                Type type = p.ParameterType;

                object value = await mvc.Services.Get(type, scope);

                if (typeof(IContainerScope).IsAssignableFrom(type))
                    values[i] = scope;
                else
                {
                    object v = await mvc.Services.Get(type, scope);
                    values[i] = v;
                }
                values[i] = value;
            }

            //if the application comes here, we are sure all parameters are created
            //now we can create instance with these parameter values
            TwinoController result = (TwinoController)Activator.CreateInstance(controllerType, values);

            //set the controller properties
            result.Request = request;
            result.Response = response;
            result.Server = mvc.Server;
            result.CurrentScope = scope;

            return result;
        }
    }
}