using System;
using System.Threading.Tasks;
using AdvancedSample.Service.Handlers;
using AdvancedSample.ServiceModels;
using Horse.Messaging.Protocol;

namespace TestService
{
	public class TestHandler : RequestHandler<SampleTestQuery, SampleTestQueryResult>
	{
		protected override Task<SampleTestQueryResult> Handle(SampleTestQuery query, HorseMessage message)
		{
			Console.WriteLine("HANDLE");
			return Task.FromResult(new SampleTestQueryResult
			{
				Bar = "Bar"
			});
		}
	}
}