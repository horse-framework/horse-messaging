using System;
using System.Linq;
using System.Threading.Tasks;
using AdvancedSample.Common.Cqrs.Infrastructure;
using AdvancedSample.Common.Infrastructure.Definitions;
using AdvancedSample.Common.Infrastructure.Exceptions;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;
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
			result.ProcessPublishResult(model);
		}

		public static async Task TryPublishJson<T>(this IHorseRouterBus bus, string routerName, T model) where T : notnull, IServiceMessage
		{
			AsyncRetryPolicy<HorseResult> policy = CreateRetryPolicy<HorseResult>();
			HorseResult result = await policy.ExecuteAsync(() => bus.PublishJson(routerName, model, true));
			result.ProcessPublishResult(model);
		}

		public static async Task TryPublishJson<T>(this IHorseRouterBus bus, string routerName, ushort contentType, T model) where T : notnull, IServiceMessage
		{
			AsyncRetryPolicy<HorseResult> policy = CreateRetryPolicy<HorseResult>();
			HorseResult result = await policy.ExecuteAsync(() => bus.PublishJson(routerName, model, contentType, true));
			result.ProcessPublishResult(model);
		}

		public static async Task TryPushJson<T>(this IHorseQueueBus bus, T model) where T : notnull, IServiceMessage
		{
			AsyncRetryPolicy<HorseResult> policy = CreateRetryPolicy<HorseResult>();
			HorseResult result = await policy.ExecuteAsync(() => bus.PushJson(model, true));
			result.ProcessPushResult(model);
		}

		public static async Task TryPushJson(this IHorseQueueBus bus, object model)
		{
			AsyncRetryPolicy<HorseResult> policy = CreateRetryPolicy<HorseResult>();
			HorseResult result = await policy.ExecuteAsync(() => bus.PushJson(model, true));
			result.ProcessPushResult(model);
		}

		public static async Task TryPushJson<T>(this IHorseQueueBus bus, string queueName, T model) where T : notnull, IServiceMessage
		{
			AsyncRetryPolicy<HorseResult> policy = CreateRetryPolicy<HorseResult>();
			HorseResult result = await policy.ExecuteAsync(() => bus.PushJson(queueName, model, true));
			result.ProcessPushResult(model);
		}

		public static async Task<TResponse> TryPublishRequestJson<TRequest, TResponse>(this IHorseRouterBus bus, TRequest model) where TRequest : notnull, IServiceMessage
		{
			AsyncRetryPolicy<HorseResult<TResponse>> policy = CreateRetryPolicy<HorseResult<TResponse>>();
			HorseResult<TResponse> result = await policy.ExecuteAsync(() => bus.PublishRequestJson<TRequest, TResponse>(model));
			return result.ProcessPublishResult(model);
		}

		public static async Task<TResponse> TryPublishRequestJson<TRequest, TResponse>(this IHorseRouterBus bus, string routerName, TRequest model) where TRequest : notnull, IServiceMessage
		{
			AsyncRetryPolicy<HorseResult<TResponse>> policy = CreateRetryPolicy<HorseResult<TResponse>>();
			HorseResult<TResponse> result = await policy.ExecuteAsync(() => bus.PublishRequestJson<TRequest, TResponse>(routerName, model));
			return result.ProcessPublishResult(model);
		}

		public static async Task<TResponse> TryPublishRequestJson<TRequest, TResponse>(this IHorseRouterBus bus, string routerName, ushort contentType, TRequest model) where TRequest : notnull, IServiceMessage
		{
			AsyncRetryPolicy<HorseResult<TResponse>> policy = CreateRetryPolicy<HorseResult<TResponse>>();
			HorseResult<TResponse> result = await policy.ExecuteAsync(() => bus.PublishRequestJson<TRequest, TResponse>(routerName, model, contentType));
			return result.ProcessPublishResult(model);
		}

		private static void ProcessPushResult<T>(this HorseResult result, T model) where T : IServiceMessage
		{
			if (result.Code == HorseResultCode.Ok) return;
			throw new HorseSentException<T>(model, result);
		}

		private static void ProcessPushResult(this HorseResult result, object model)
		{
			if (result.Code == HorseResultCode.Ok) return;
			throw new HorseSentException(model, result);
		}

		private static void ProcessPublishResult<T>(this HorseResult result, T model) where T : notnull, IServiceMessage
		{
			if (result.Code == HorseResultCode.Ok) return;
			throw new HorseSentException<T>(model, result);
		}

		private static TResponse ProcessPublishResult<TRequest, TResponse>(this HorseResult<TResponse> result, TRequest model) where TRequest : notnull, IServiceMessage
		{
			if (result.Code == HorseResultCode.Ok) return result.Model;
			throw new HorseSentException<TRequest>(model, result);
		}

		private static AsyncRetryPolicy<T> CreateRetryPolicy<T>() where T : HorseResult
		{
			return Policy.HandleResult<T>(r => r.Code != HorseResultCode.Ok)
						 .WaitAndRetryAsync(new int[5].Select((_, i) => TimeSpan.FromSeconds(i)));
		}
	}
}