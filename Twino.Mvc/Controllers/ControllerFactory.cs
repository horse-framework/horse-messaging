using System;
using System.Reflection;
using Twino.Core.Http;
using Twino.Mvc.Services;

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
        public TwinoController CreateInstance(TwinoMvc mvc, Type controllerType, HttpRequest request, HttpResponse response, IContainerScope scope)
        {
            ConstructorInfo[] constructors = controllerType.GetConstructors();

            //some constructors may have not-registered parameters.
            //these constructor must not break the operation.
            //we need to find the right constructor (right means, ctor that we can provide all parameters from the service container)
            foreach (ConstructorInfo constructor in constructors)
            {
                ParameterInfo[] parameters = constructor.GetParameters();
                object[] values = new object[parameters.Length];
                bool skip = false;

                //each parameter must be provided from the service container
                for (int i = 0; i < parameters.Length; i++)
                {
                    ParameterInfo p = parameters[i];
                    Type type = p.ParameterType;

                    object value = mvc.Services.Get(type, scope);

                    //if the parameter could not provide, we need to skip this constructor
                    if (value == null)
                    {
                        skip = true;
                        break;
                    }

                    values[i] = value;
                }

                if (skip)
                    continue;

                //if the application comes here, we are sure all parameters are created
                //now we can create instance with these parameter values
                TwinoController result = (TwinoController) Activator.CreateInstance(controllerType, values);

                //set the controller properties
                result.Request = request;
                result.Response = response;
                result.Server = mvc.Server;

                return result;
            }

            return null;
        }
    }
}