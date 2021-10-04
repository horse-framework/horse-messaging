using System.Threading.Tasks;
using AdvancedSample.Service.Handlers;
using AdvancedSample.ServiceModels;

namespace TestService
{
	public class TestEventHandler : EventHandler<SampleTestEvent>
	{
		protected override async Task Execute(SampleTestEvent command)
		{
			await Task.Delay(40000);
		}
	}
}