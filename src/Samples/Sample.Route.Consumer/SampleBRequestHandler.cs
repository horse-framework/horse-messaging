using System;
using System.Threading.Tasks;
using Sample.Route.Models;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Annotations;
using Twino.Protocols.TMQ;

namespace Sample.Route.Consumer
{
	[AutoAck]
	[AutoNack]
	public class SampleBRequestHandler: ITwinoRequestHandler<SampleBRequest, SampleResult>
	{
		public Task<SampleResult> Handle(SampleBRequest request, TmqMessage rawMessage, TmqClient client)
		{
			var result = new SampleResult
			{
				Message = $"SUCCESS => {request.Name} | {request.Guid}"
			};
			return Task.FromResult(result);
		}

		public Task<ErrorResponse> OnError(Exception exception, SampleBRequest request, TmqMessage rawMessage, TmqClient client)
		{
			throw new NotImplementedException();
		}
	}
}