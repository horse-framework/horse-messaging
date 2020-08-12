using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sample.Route.Models;
using Twino.Client.TMQ;
using Twino.Protocols.TMQ;

namespace Sample.Route.Consumer2
{
	public class SampleBRequestHandler: ITwinoRequestHandler<SampleBRequest, List<SampleResult>>
	{
		public Task<List<SampleResult>> Handle(SampleBRequest request, TmqMessage rawMessage, TmqClient client)
		{
			var result = new List<SampleResult>()
			{
				new SampleResult {Message = $"SUCCESS => {request.Name} | {request.Guid}"},
				new SampleResult {Message = $"SUCCESS => {request.Name} | {request.Guid}"},
				new SampleResult {Message = $"SUCCESS => {request.Name} | {request.Guid}"},
				new SampleResult {Message = $"SUCCESS => {request.Name} | {request.Guid}"},
			};
			return Task.FromResult(result);
		}

		public Task<ErrorResponse> OnError(Exception exception, SampleBRequest request, TmqMessage rawMessage, TmqClient client)
		{
			throw new NotImplementedException();
		}
	}
}