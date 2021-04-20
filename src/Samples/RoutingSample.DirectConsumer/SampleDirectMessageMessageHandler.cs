using System;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using RoutingSample.Models;

namespace RoutingSample.DirectConsumer
{
	public class SampleDirectMessageMessageHandler : BaseDirectMessageHandler<SampleMessage>
	{
		protected override async Task Handle(SampleMessage model)
		{
			GiveMeGuidRequest request = new GiveMeGuidRequest
			{
				Foo = "Hello from sample direct message consumer"
			};
			HorseResult<GiveMeGuidResponse> guidResponse = await Program.RouteBus.PublishRequestJson<GiveMeGuidRequest, GiveMeGuidResponse>(request);
			Console.WriteLine($"SAMPLE DIRECT MESSAGE CONSUMED [{guidResponse.Model.Guid}]");
		}
	}
}