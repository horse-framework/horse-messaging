using System;
using System.Threading.Tasks;
using RoutingSample.Models;
using Twino.Protocols.TMQ;

namespace RoutingSample.DirectConsumer
{
	public class SampleDirectMessageConsumer : BaseDirectConsumer<SampleMessage>
	{
		protected override async Task Handle(SampleMessage model)
		{
			GiveMeGuidRequest request = new GiveMeGuidRequest
			{
				Foo = "Hello from sample direct message consumer"
			};
			TwinoResult<GiveMeGuidResponse> guidResponse = await Program.RouteBus.PublishRequestJson<GiveMeGuidRequest, GiveMeGuidResponse>(request);
			Console.WriteLine($"SAMPLE DIRECT MESSAGE CONSUMED [{guidResponse.Model.Guid}]");
		}
	}
}