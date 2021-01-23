using System;
using System.Threading.Tasks;
using RoutingSample.Models;
using Horse.Mq.Client;
using Horse.Protocols.Hmq;

namespace RoutingSample.InternalService
{
	public class GiveMeGuidRequestHandler : IHorseRequestHandler<GiveMeGuidRequest, GiveMeGuidResponse>
	{
		public Task<GiveMeGuidResponse> Handle(GiveMeGuidRequest request, HorseMessage rawMessage, HorseClient client)
		{
			Console.WriteLine(request.Foo);
			Console.WriteLine($"GIVE ME GUID REQUEST HANDLED");
			return Task.FromResult(new GiveMeGuidResponse {Guid = Guid.NewGuid()});
		}

		public Task<ErrorResponse> OnError(Exception exception, GiveMeGuidRequest request, HorseMessage rawMessage, HorseClient client)
		{
			return Task.FromResult(new ErrorResponse {Reason = "Something was wrong.", ResultCode = HorseResultCode.InternalServerError});
		}
	}
}