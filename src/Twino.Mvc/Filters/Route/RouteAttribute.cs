using System;

namespace Twino.Mvc.Filters.Route
{
    /// <summary>
    /// Route attribute for the controller classes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RouteAttribute : Attribute
    {
        /// <summary>
        /// Controller route pattern.
        /// For controller name type "[controller]"
        /// </summary>
        public string Pattern { get; }

        /// <summary>
        /// Creates new default controller route attribute with "[controller]" pattern
        /// </summary>
        public RouteAttribute()
        {
            Pattern = "[controller]";
        }

        /// <summary>
        /// Creates new route attribute with specified pattern
        /// </summary>
        public RouteAttribute(string pattern)
        {
            Pattern = pattern;
        }
    }
}