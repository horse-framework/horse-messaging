using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sample.Route.Models;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Bus;
using Twino.Protocols.TMQ;

namespace Sample.Route.Consumer1
{
	public class SampleARequestHandler: ITwinoRequestHandler<SampleARequest, List<SampleResult>>
	{
		private readonly ITwinoRouteBus _bus;

		public SampleARequestHandler(ITwinoRouteBus bus)
		{
			_bus = bus;
		}

		public async Task<List<SampleResult>> Handle(SampleARequest request, TmqMessage rawMessage, TmqClient client)
		{
			var requestB = new SampleBRequest
			{
				Name = request.Name,
				Guid = request.Guid
			};
			var result = await _bus.Execute<SampleBRequest, List<SampleResult>>(requestB);
			return result;
		}

		public Task<ErrorResponse> OnError(Exception exception, SampleARequest request, TmqMessage rawMessage, TmqClient client)
		{
			throw new NotImplementedException();
		}
	}
}