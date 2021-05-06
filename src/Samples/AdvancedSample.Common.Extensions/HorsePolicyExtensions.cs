using System;
using System.Linq;
using System.Threading.Tasks;
using AdvancedSample.Common.Cqrs.Infrastructure;
using AdvancedSample.Common.Infrastructure.Exceptions;
using Horse.Messaging.Client.Routers;
using Horse.Messaging.Protocol;
using Polly;
using Polly.Retry;

namespace AdvancedSample.Common.Extensions
{
	internal static class HorsePolicyExtensions
	{
		public static async Task TryPublishJson<T>(this IHorseRouterBus bus, T model) where T : notnull, IServiceMessage
		{
			AsyncRetryPolicy<HorseResult> policy = CreateRetryPolicy<HorseResult>();
			HorseResult result = await policy.ExecuteAsync(() => bus.PublishJson(model, true));
			result.ProcessHorseResult(model);
		}

		public static async Task TryPublishJson<T>(this IHorseRouterBus bus, string routerName, T model) where T : notnull, IServiceMessage
		{
			AsyncRetryPolicy<HorseResult> policy = CreateRetryPolicy<HorseResult>();
			HorseResult result = await policy.ExecuteAsync(() => bus.PublishJson(routerName, model, true));
			result.ProcessHorseResult(model);
		}

		public static async Task TryPublishJson<T>(this IHorseRouterBus bus, string routerName, ushort contentType, T model) where T : notnull, IServiceMessage
		{
			AsyncRetryPolicy<HorseResult> policy = CreateRetryPolicy<HorseResult>();
			HorseResult result = await policy.ExecuteAsync(() => bus.PublishJson(routerName, model, contentType, true));
			result.ProcessHorseResult(model);
		}

		public static async Task<TResponse> TryPublishRequestJson<TRequest, TResponse>(this IHorseRouterBus bus, TRequest model) where TRequest : notnull, IServiceMessage
		{
			AsyncRetryPolicy<HorseResult<TResponse>> policy = CreateRetryPolicy<HorseResult<TResponse>>();
			HorseResult<TResponse> result = await policy.ExecuteAsync(() => bus.PublishRequestJson<TRequest, TResponse>(model));
			return result.ProcessHorseResult(model);
		}

		public static async Task<TResponse> TryPublishRequestJson<TRequest, TResponse>(this IHorseRouterBus bus, string routerName, TRequest model) where TRequest : notnull, IServiceMessage
		{
			AsyncRetryPolicy<HorseResult<TResponse>> policy = CreateRetryPolicy<HorseResult<TResponse>>();
			HorseResult<TResponse> result = await policy.ExecuteAsync(() => bus.PublishRequestJson<TRequest, TResponse>(routerName, model));
			return result.ProcessHorseResult(model);
		}

		public static async Task<TResponse> TryPublishRequestJson<TRequest, TResponse>(this IHorseRouterBus bus, string routerName, ushort contentType, TRequest model) where TRequest : notnull, IServiceMessage
		{
			AsyncRetryPolicy<HorseResult<TResponse>> policy = CreateRetryPolicy<HorseResult<TResponse>>();
			HorseResult<TResponse> result = await policy.ExecuteAsync(() => bus.PublishRequestJson<TRequest, TResponse>(routerName, model, contentType));
			return result.ProcessHorseResult(model);
		}

		private static void ProcessHorseResult<T>(this HorseResult result, T model) where T : notnull, IServiceMessage
		{
			if (result.Code == HorseResultCode.Ok) return;
			throw new HorsePublishException<T>(model, result);
		}

		private static TResponse ProcessHorseResult<TRequest, TResponse>(this HorseResult<TResponse> result, TRequest model) where TRequest : notnull, IServiceMessage
		{
			if (result.Code == HorseResultCode.Ok) return result.Model;
			throw new HorsePublishException<TRequest>(model, result);
		}

		private static AsyncRetryPolicy<T> CreateRetryPolicy<T>() where T : HorseResult
		{
			return Policy.HandleResult<T>(r => r.Code != HorseResultCode.Ok)
						 .WaitAndRetryAsync(new int[5].Select((_, i) => TimeSpan.FromSeconds(i)));
		}
	}
}