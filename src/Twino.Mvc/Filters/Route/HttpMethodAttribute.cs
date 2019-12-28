using System;

namespace Twino.Mvc.Filters.Route
{
    /// <summary>
    /// Base class for all HTTP Method attribute.
    /// For other HTTP methods (if Twino MVC does not have the specified method attribute)
    /// You can use this method and pass the upper-case string method value to method parameter in constructor.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class HttpMethodAttribute : Attribute
    {
        /// <summary>
        /// HTTP Method
        /// </summary>
        public string Method { get; }

        /// <summary>
        /// Route pattern for the action route parameters type {id}, for optional parameters type {?id}
        /// </summary>
        public string Pattern { get; internal set; }

        public HttpMethodAttribute(string method, string pattern)
        {
            Method = method;
            Pattern = pattern;
        }
    }
}
