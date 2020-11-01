using System;
using System.Threading.Tasks;
using RoutingSample.Models;

namespace RoutingSample.DirectConsumer
{
	public class SampleDirectMessageConsumer : BaseDirectConsumer<SampleMessage>
	{
		protected override Task Handle(SampleMessage model)
		{
			Console.WriteLine("SAMPLE DIRECT MESSAGE CONSUMED");
			return Task.CompletedTask;
		}
	}
}