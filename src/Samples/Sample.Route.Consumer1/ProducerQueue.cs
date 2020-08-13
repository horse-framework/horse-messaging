using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sample.Route.Models;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Bus;
using Twino.Protocols.TMQ;

namespace Sample.Route.Consumer1
{
	public class ProducerQueue: IQueueConsumer<ProduceRequestA>
	{
		private readonly ITwinoRouteBus _bus;

		public ProducerQueue(ITwinoRouteBus bus)
		{
			_bus = bus;
		}

		public async Task Consume(TmqMessage message, ProduceRequestA model, TmqClient client)
		{
			var request = new SampleARequest
			{
				Name = "A-REQUEST",
				Guid = Guid.NewGuid()
			};
			var result = await _bus.Execute<SampleARequest, List<SampleResult>>(request);
		}
	}
}