using System;
using System.Threading.Tasks;
using Twino.Client.TMQ.Bus;
using Twino.Protocols.TMQ;

namespace Sample.Route.Models
{
	public static class Extensions
	{
		public static async Task<TResponse> Execute<TRequest, TResponse>(this ITwinoRouteBus bus, TRequest request) where TResponse: class
		{
			TwinoResult<TResponse> response = await bus.PublishRequestJson<TRequest, TResponse>(request);

			return response.Code switch
			{
				TwinoResultCode.Ok        => response.Model,
				TwinoResultCode.NoContent => response.Model,
				var _                     => default
			};
		}
	}
}