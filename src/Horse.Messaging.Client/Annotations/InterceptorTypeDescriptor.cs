using System;
using Horse.Messaging.Client.Interceptors;

namespace Horse.Messaging.Client.Annotations;

/// <summary>
/// Descriptor for interceptor
/// </summary>
public class InterceptorTypeDescriptor
{
    /// <summary>
    /// Interceptor type
    /// </summary>
    public Type InterceptorType { get; init; }

    /// <summary>
    /// Execution order
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// Interceptor method
    /// </summary>
    public bool RunBefore { get; init; }

    /// <summary>
    /// Singleton instance
    /// </summary>
    internal IHorseInterceptor Instance { get; set; }

    private InterceptorTypeDescriptor()
    {
    }

    /// <summary>
    /// Interceptor type descriptor factory
    /// </summary>
    /// <returns></returns>
    public static InterceptorTypeDescriptor Create(InterceptorAttribute attr, bool createInstance)
    {
        InterceptorTypeDescriptor descriptor = new()
        {
            InterceptorType = attr.InterceptorType,
            Order = attr.Order,
            RunBefore = attr.RunBefore,
        };
        if (createInstance)
            descriptor.Instance = TryCreateInstance(attr.InterceptorType);
        return descriptor;
    }

    internal static InterceptorTypeDescriptor Clone(InterceptorTypeDescriptor descriptor, bool createInstanceIfMissing)
    {
        InterceptorTypeDescriptor copy = new()
        {
            InterceptorType = descriptor.InterceptorType,
            Order = descriptor.Order,
            RunBefore = descriptor.RunBefore,
            Instance = descriptor.Instance
        };

        if (copy.Instance == null && createInstanceIfMissing)
            copy.Instance = TryCreateInstance(copy.InterceptorType);

        return copy;
    }

    private static IHorseInterceptor TryCreateInstance(Type interceptorType)
    {
        if (interceptorType.GetConstructor(Type.EmptyTypes) == null)
            return null;

        return (IHorseInterceptor) Activator.CreateInstance(interceptorType);
    }
}
