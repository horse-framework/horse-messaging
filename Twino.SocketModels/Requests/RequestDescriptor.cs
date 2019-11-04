using System;
using System.Threading.Tasks;

namespace Twino.SocketModels.Requests
{
    /// <summary>
    /// Real request descriptor class keeps response actions and calls them
    /// </summary>
    internal class RequestDescriptor<TRequest, TResponse> : RequestDescriptor
        where TRequest : ISocketModel, new()
        where TResponse : ISocketModel, new()
    {
        public Func<TRequest, TResponse> Action { get; set; }
        public Func<TRequest, Task<TResponse>> ActionAsync { get; set; }

        public override async Task<object> Do(object request)
        {
            if (IsAsync)
            {
                TResponse response = await ActionAsync((TRequest) request);
                return response;
            }

            return Action((TRequest) request);
        }
    }

    /// <summary>
    /// Used for define waiting request type to response by Request Manager.
    /// </summary>
    internal abstract class RequestDescriptor
    {
        /// <summary>
        /// Request type code
        /// </summary>
        public int RequestNo { get; set; }

        /// <summary>
        /// Request model type
        /// </summary>
        public Type RequestType { get; set; }

        /// <summary>
        /// Response type code
        /// </summary>
        public int ResponseNo { get; set; }

        /// <summary>
        /// Response model type
        /// </summary>
        public Type ResponseType { get; set; }

        /// <summary>
        /// True, if action is async function
        /// </summary>
        internal bool IsAsync { get; set; }

        public virtual async Task<object> Do(object request)
        {
            return await Task.FromResult((object) null);
        }
    }
}