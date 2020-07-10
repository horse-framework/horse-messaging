using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Twino.Ioc.Exceptions;

namespace Twino.Ioc
{
    internal static class Helpers
    {
        internal static string ToTypeString(this Type type)
        {
            if (!type.IsGenericType)
                return type.FullName;

            StringBuilder builder = new StringBuilder();

            builder.Append(type.Namespace);
            builder.Append(".");
            builder.Append(type.Name.Substring(0, type.Name.IndexOf('`')));
            builder.Append("<");

            Type[] generics = type.GetGenericArguments();
            for (int i = 0; i < generics.Length; i++)
            {
                Type g = generics[i];
                builder.Append(g.ToTypeString());
                if (i < generics.Length - 1)
                    builder.Append(",");
            }

            builder.Append(">");

            return builder.ToString();
        }

        /// <summary>
        /// Finds all usable constructors of the type
        /// </summary>
        internal static ConstructorInfo[] FindUsableConstructors(Type type)
        {
            ConstructorInfo[] ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

            if (ctors.Length == 0)
                throw new IocConstructorException($"{type.ToTypeString()} does not have a public constructor");

            if (ctors.Length == 1)
                return ctors;

            Dictionary<int, ConstructorInfo> dict = new Dictionary<int, ConstructorInfo>();
            foreach (ConstructorInfo c in ctors)
            {
                TwinoIocConstructorAttribute attr = c.GetCustomAttribute<TwinoIocConstructorAttribute>();
                if (attr == null)
                    continue;

                if (dict.ContainsKey(attr.Priority))
                    throw new IocConstructorException($"{type.ToTypeString()} have multiple same priority TwinoIocConstructorAttribute with value: {attr.Priority}");

                dict.Add(attr.Priority, c);
            }

            if (dict.Count == 0)
                throw new IocConstructorException($"{type.ToTypeString()} has multiple constructors but TwinoIocConstructorAttribute could not be found");

            return dict.OrderBy(x => x.Key).Select(x => x.Value).ToArray();
        }
    }
}