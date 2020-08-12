using System;
using System.Threading.Tasks;
using Sample.Route.Models;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Annotations;
using Twino.Client.TMQ.Bus;
using Twino.Protocols.TMQ;

namespace Sample.Route.Consumer
{
	[AutoAck]
	[AutoNack]
	public class SampleARequestHandler: ITwinoRequestHandler<SampleARequest, SampleResult>
	{
		private readonly ITwinoRouteBus _bus;

	

		public Task<SampleResult> Handle(SampleARequest request, TmqMessage rawMessage, TmqClient client)
		{
			var requestB = new SampleBRequest
			{
				Name = request.Name,
				Guid = request.Guid
			};
			return _bus.Execute<SampleBRequest, SampleResult>(requestB);
		}

		public Task<ErrorResponse> OnError(Exception exception, SampleARequest request, TmqMessage rawMessage, TmqClient client)
		{
			throw new NotImplementedException();
		}
	}
}