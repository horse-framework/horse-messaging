using System;

namespace Twino.Mvc.Filters.Route
{
    /// <summary>
    /// Attribute for action methods routed with HTTP DELETE method
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HttpDeleteAttribute : HttpMethodAttribute
    {

        public HttpDeleteAttribute() : this(null)
        {
        }

        public HttpDeleteAttribute(string pattern) : base("DELETE", pattern)
        {
        }

    }
}
