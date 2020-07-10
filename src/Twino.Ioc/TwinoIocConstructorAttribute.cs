using System;

namespace Twino.Ioc
{
    /// <summary>
    /// That attribute is for marking constructors of types that are used for creating instance of implementation types.
    /// </summary>
    [AttributeUsage(AttributeTargets.Constructor)]
    public class TwinoIocConstructorAttribute : Attribute
    {
        /// <summary>
        /// Constructor usage priority.
        /// Twino IOC tries lower priority values first.
        /// If that constructor parameters are not registered,
        /// lower priority attributes are used.
        /// </summary>
        public int Priority { get; }

        /// <summary>
        /// Marks a constructor to tell Twino IOC, that constructor will be used for injection.
        /// If there are multiple constructors with that attribute, all must have different priority values.
        /// 0 is highest priority. Priority lowers when value increases.
        /// If there are multiple attributes on same type with same values, an exception is thrown.
        /// </summary>
        public TwinoIocConstructorAttribute(int priority = 0)
        {
            Priority = priority;
        }
    }
}