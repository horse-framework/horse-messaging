using System;
using System.Threading.Tasks;
using AdvancedSample.ServiceModels;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Direct;
using Horse.Messaging.Protocol;

namespace TestService
{
	public class TestHandler : IHorseRequestHandler<SampleTestQuery, SampleTestQueryResult>
	{
		public Task<SampleTestQueryResult> Handle(SampleTestQuery request, HorseMessage rawMessage, HorseClient client)
		{
			return Task.FromResult(new SampleTestQueryResult
			{
				Bar = "Bar"
			});
		}

		public Task<ErrorResponse> OnError(Exception exception, SampleTestQuery request, HorseMessage rawMessage, HorseClient client)
		{
			return Task.FromResult(new ErrorResponse
			{
				Reason = exception.Message,
				ResultCode = HorseResultCode.InternalServerError
			});
		}
	}
}