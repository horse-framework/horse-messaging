using System;
using System.Collections.Generic;
using System.Reflection;

namespace Twino.Mvc.Controllers
{
    /// <summary>
    /// Resource types for the action method parameter
    /// </summary>
    public enum ParameterSource
    {
        /// <summary>
        /// Parameter source type isn't specified. Will be search from the route.
        /// </summary>
        None,

        /// <summary>
        /// Parameter will be read from the query string of the request.
        /// </summary>
        QueryString,

        /// <summary>
        /// Parameter will be read from the content body of the request.
        /// Request content must be application/form
        /// </summary>
        Form,

        /// <summary>
        /// Parameter will be read from the content body of the request.
        /// Type can be JSON or XML.
        /// Action method can contain only ONE Body source parameter.
        /// </summary>
        Body,

        /// <summary>
        /// Parameter will be read from the route URL.
        /// </summary>
        Route,

        /// <summary>
        /// Parameter wil be read from the HTTP Header key/value pairs of the request.
        /// </summary>
        Header
    }

    /// <summary>
    /// Represents a parameter of an action.
    /// Created after MVC initialized and is kept full lifetime in the MVC application.
    /// Keeps all request source and method parameter information,
    /// how the parameter will be read from the request and how to bind and which method parameter.
    /// </summary>
    public class ActionParameter
    {
        /// <summary>
        /// Action Method parameter name
        /// </summary>
        public string ParameterName { get; set; }

        /// <summary>
        /// Request parameter name.
        /// This is not action method parameter name.
        /// This is the key or name of the source.
        /// </summary>
        public string FromName { get; set; }

        /// <summary>
        /// Action Method order index for the parameter.
        /// When the method is invoked with reflection,
        /// parameters must be passed as object array.
        /// So we should now which value must be in which index of the array
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Action Method parameter type
        /// </summary>
        public Type ParameterType { get; set; }

        /// <summary>
        /// Parameter source, where the value read from
        /// </summary>
        public ParameterSource Source { get; set; }

        /// <summary>
        /// If true, the type has underlying type.
        /// Usually true when the type is int?, short?, etc.
        /// </summary>
        public bool Nullable { get; set; }
        
        /// <summary>
        /// If true, property is a class
        /// </summary>
        public bool IsClass { get; set; }

        /// <summary>
        /// If parameter is a class, property info list of that class. otherwise, null.
        /// </summary>
        public Dictionary<string, PropertyInfo> ClassProperties { get; } = new Dictionary<string, PropertyInfo>(StringComparer.InvariantCultureIgnoreCase);
    }
}
