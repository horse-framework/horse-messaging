using System;
using System.Collections.Generic;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Internal;

namespace Horse.Messaging.Client.Direct;

internal class DirectHandlerRegistration
{
    /// <summary>
    /// Subscribed content type
    /// </summary>
    public ushort ContentType { get; set; }

    /// <summary>
    /// Direct Handler type
    /// </summary>
    public Type HandlerType { get; set; }

    /// <summary>
    /// Direct message type
    /// </summary>
    public Type MessageType { get; set; }

    /// <summary>
    /// Request handler's response type
    /// </summary>
    public Type ResponseType { get; set; }

    /// <summary>
    /// Interceptor descriptors
    /// </summary>
    internal List<InterceptorTypeDescriptor> IntercetorDescriptors { get; } = new();
        
    /// <summary>
    /// Consumer executor
    /// </summary>
    internal ExecutorBase ConsumerExecuter { get; set; }


}