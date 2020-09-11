using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sample.Route.Models;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Bus;
using Twino.Protocols.TMQ;

namespace Sample.Route.Consumer1
{
	public class SampleARequestHandler : ITwinoRequestHandler<SampleARequest, List<SampleResult>>
	{
		private readonly ITwinoRouteBus _bus;

		public SampleARequestHandler(ITwinoRouteBus bus)
		{
			_bus = bus;
		}

		public Task<List<SampleResult>> Handle(SampleARequest request, TmqMessage rawMessage, TmqClient client)
		{
			Console.Write(request.Name);
			return Task.FromResult(new List<SampleResult> {new SampleResult {Message = "rmessage"}});
		}

		public Task<ErrorResponse> OnError(Exception exception, SampleARequest request, TmqMessage rawMessage, TmqClient client)
		{
			throw new NotImplementedException();
		}
	}
}