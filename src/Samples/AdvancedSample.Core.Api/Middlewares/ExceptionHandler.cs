using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Horse.Messaging.Client.Queues;
using Microsoft.AspNetCore.Http;

namespace AdvancedSample.Core.Api.Middlewares
{
	public class ExceptionHandler
	{
		private readonly RequestDelegate _next;
		private IHorseQueueBus _bus;

		public ExceptionHandler(RequestDelegate next)
		{
			_next = next;
		}

		public async Task Invoke(HttpContext context, IHorseQueueBus bus)
		{
			try
			{
				_bus = bus;
				await _next(context);
			}
			catch (Exception exception)
			{
				await Handle(context, exception);
			}
		}

		private static Task Handle(HttpContext context, Exception exception)
		{
			return WriteResponseAsync(context, HttpStatusCode.InternalServerError, exception);
		}

		private static Task WriteResponseAsync(HttpContext context, HttpStatusCode code, Exception exception)
		{
			context.Response.ContentType = "application/json";
			context.Response.StatusCode = (int) code;
			return context.Response.WriteAsync(exception.Message, Encoding.UTF8);
		}
	}
}