using System;
using System.Threading.Tasks;
using AdvancedSample.Common.Cqrs.Infrastructure;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Direct;
using Horse.Messaging.Protocol;

namespace AdvancedSample.Common.Infrastructure.Handlers
{
	[Retry(count: 5, delayBetweenRetries: 250)]
	public abstract class QueryHandler<TRequest, TResponse> : IHorseRequestHandler<TRequest, TResponse> where TRequest : IServiceQuery
	{
		public Task<TResponse> Handle(TRequest request, HorseMessage rawMessage, HorseClient client)
		{
			return Handle(request);
		}

		public Task<ErrorResponse> OnError(Exception exception, TRequest request, HorseMessage rawMessage, HorseClient client)
		{
			return Task.FromResult(new ErrorResponse
			{
				Reason = exception.Message,
				ResultCode = HorseResultCode.Failed
			});
		}

		protected abstract Task<TResponse> Handle(TRequest query);
	}
}