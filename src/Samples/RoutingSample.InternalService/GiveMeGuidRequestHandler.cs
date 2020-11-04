using System;
using System.Threading.Tasks;
using RoutingSample.Models;
using Twino.MQ.Client;
using Twino.Protocols.TMQ;

namespace RoutingSample.InternalService
{
	public class GiveMeGuidRequestHandler : ITwinoRequestHandler<GiveMeGuidRequest, GiveMeGuidResponse>
	{
		public Task<GiveMeGuidResponse> Handle(GiveMeGuidRequest request, TwinoMessage rawMessage, TmqClient client)
		{
			Console.WriteLine(request.Foo);
			Console.WriteLine($"GIVE ME GUID REQUEST HANDLED");
			return Task.FromResult(new GiveMeGuidResponse {Guid = Guid.NewGuid()});
		}

		public Task<ErrorResponse> OnError(Exception exception, GiveMeGuidRequest request, TwinoMessage rawMessage, TmqClient client)
		{
			return Task.FromResult(new ErrorResponse {Reason = "Something was wrong.", ResultCode = TwinoResultCode.InternalServerError});
		}
	}
}