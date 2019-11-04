using System;
using System.Threading.Tasks;
using Twino.Core;

namespace Twino.SocketModels.Requests
{
    public static class TwinoRequest
    {
        /// <summary>
        /// async unique number
        /// </summary>
        private static volatile int UNIQUE_HELPER = 100000;

        /// <summary>
        /// Creates a request to the server and sends the model in specified timeout (in seconds).
        /// Response will be received via returned task
        /// </summary>
        public static async Task<SocketResponse<TResponse>> Request<TResponse>(this SocketBase sender, ISocketModel model)
            where TResponse : ISocketModel, new()
        {
            return await Request<TResponse>(sender, model, RequestManager.DefaultTimeout);
        }

        /// <summary>
        /// Creates a request to the server and sends the model in specified timeout (in seconds).
        /// Response will be received via returned task
        /// </summary>
        public static async Task<SocketResponse<TResponse>> Request<TResponse>(this SocketBase sender, ISocketModel model, int timeoutSeconds)
            where TResponse : ISocketModel, new()
        {
            RequestClientHandler handler = RequestClientPool.Instance.GetHandler(sender);

            TaskCompletionSource<SocketResponse<TResponse>> completionSource = new TaskCompletionSource<SocketResponse<TResponse>>();
            RequestHeader header = CreateRequest<TResponse>(model);

            PendingRequest<TResponse> pendingRequest = new PendingRequest<TResponse>();
            pendingRequest.Header = header;
            pendingRequest.Deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            pendingRequest.CompletionSource = completionSource;
            pendingRequest.Sender = sender;

            if (!sender.IsConnected)
            {
                pendingRequest.CompleteAsError();
                return await completionSource.Task;
            }

            byte[] prepared = await TwinoRequestSerializer.SerializeRequest(header, model);
            handler.Handle(pendingRequest);
            sender.Send(prepared);

            return await completionSource.Task;
        }

        /// <summary>
        /// Creates request header with sending and receiving models.
        /// </summary>
        private static RequestHeader CreateRequest<TResponse>(ISocketModel request) where TResponse : ISocketModel, new()
        {
            TResponse response = new TResponse();
            return new RequestHeader
                   {
                       Unique = CreateUnique(),
                       RequestType = request.Type,
                       ResponseType = response.Type
                   };
        }

        /// <summary>
        /// Creates really unique number
        /// </summary>
        private static string CreateUnique()
        {
            string g1 = Guid.NewGuid().ToString();
            
            int v = UNIQUE_HELPER;
            UNIQUE_HELPER++;
            if (UNIQUE_HELPER > 999999)
                UNIQUE_HELPER = 100000;

            string g2 = Guid.NewGuid().ToString();

            return $"{g1}-{g2}-{v}";
        }
    }
}