using System;
using System.Threading.Tasks;
using Twino.Core;
using Twino.JsonModel;

namespace Twino.Protocols.WebSocket.Requests
{
    /// <summary>
    /// After a request is sent, to handle the response, information about the request must be kept.
    /// This class defines the properties which are kept for the request
    /// </summary>
    internal class PendingRequest<TModel> : PendingRequest
        where TModel : ISerializableModel, new()
    {
        /// <summary>
        /// Async response task completion source
        /// </summary>
        public TaskCompletionSource<SocketResponse<TModel>> CompletionSource { get; set; }

        /// <summary>
        /// Response model
        /// </summary>
        public SocketResponse<TModel> Response { get; set; }

        public PendingRequest()
        {
            ModelType = typeof(TModel);
        }

        /// <summary>
        /// Completes the request as successful with model
        /// </summary>
        public override void CompleteAsSuccessful(object model)
        {
            Complete((TModel) model, ResponseStatus.Success);
        }

        /// <summary>
        /// Completes the request as timed out
        /// </summary>
        public override void CompleteAsTimeout()
        {
            Complete(default, ResponseStatus.Timeout);
        }

        /// <summary>
        /// Completes the request as error
        /// </summary>
        public override void CompleteAsError()
        {
            Complete(default, ResponseStatus.ConnectionError);
        }

        /// <summary>
        /// Completes the request as failed
        /// </summary>
        public override void CompleteAsFailed()
        {
            Complete(default, ResponseStatus.Failed);
        }

        /// <summary>
        /// Completes the pending request process and fires task completion source event
        /// </summary>
        private void Complete(TModel model, ResponseStatus status)
        {
            if (Finished)
                return;

            Finished = true;

            Response = new SocketResponse<TModel>();
            Response.Unique = Header.Unique;
            Response.RequestType = Header.RequestType;
            Response.ResponseType = Header.ResponseType;
            Response.Model = model;
            Response.Status = status;

            CompletionSource.SetResult(Response);
        }
    }

    /// <summary>
    /// Base class for real pending request generic class.
    /// In RequestManager usage, we cannot know that the generic type is but we need to set generic values.
    /// This class allows us to do this with abstract methods.
    /// </summary>
    internal abstract class PendingRequest
    {
        /// <summary>
        /// Request unique id, request and response type information
        /// </summary>
        public RequestHeader Header { get; set; }

        /// <summary>
        /// Timeout date for the request
        /// </summary>
        public DateTime Deadline { get; set; }

        /// <summary>
        /// Request's TCP client
        /// </summary>
        public SocketBase Sender { get; set; }

        /// <summary>
        /// Response model type
        /// </summary>
        public Type ModelType { get; protected set; }

        /// <summary>
        /// True if the request's process is completed
        /// </summary>
        public bool Finished { get; set; }

        /// <summary>
        /// Completes the request as successful with model
        /// </summary>
        public abstract void CompleteAsSuccessful(object model);

        /// <summary>
        /// Completes the request as timed out
        /// </summary>
        public abstract void CompleteAsTimeout();

        /// <summary>
        /// Completes the request as error
        /// </summary>
        public abstract void CompleteAsError();

        /// <summary>
        /// Completes the request as failed
        /// </summary>
        public abstract void CompleteAsFailed();
    }
}