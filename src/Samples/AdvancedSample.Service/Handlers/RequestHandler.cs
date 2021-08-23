using System;
using System.Threading.Tasks;
using AdvancedSample.Service.Interceptors;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Direct;
using Horse.Messaging.Protocol;

namespace AdvancedSample.Service.Handlers
{
	[Interceptor(typeof(TestInterceptor))]
	public abstract class RequestHandler<TRequest, TResponse> : IHorseRequestHandler<TRequest, TResponse>
	{
		public async Task<TResponse> Handle(TRequest query, HorseMessage message, HorseClient client)
		{
			TResponse result = await Handle(query, message);
			return result;
		}

		public Task<ErrorResponse> OnError(Exception exception, TRequest request, HorseMessage rawMessage, HorseClient client)
		{
			return Task.FromResult(new ErrorResponse
			{
				Reason = exception.Message,
				ResultCode = HorseResultCode.InternalServerError
			});
		}

		protected abstract Task<TResponse> Handle(TRequest query, HorseMessage message);
	}
}