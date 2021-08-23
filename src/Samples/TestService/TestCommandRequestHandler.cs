using System;
using System.Threading.Tasks;
using AdvancedSample.Service.Handlers;
using AdvancedSample.ServiceModels;

namespace TestService
{
	public class TestCommandHandler : CommandHandler<SampleTestCommand>
	{
		protected override Task Execute(SampleTestCommand command)
		{
			Console.WriteLine("EXECUTE");
			return Task.CompletedTask;
		}
	}
}