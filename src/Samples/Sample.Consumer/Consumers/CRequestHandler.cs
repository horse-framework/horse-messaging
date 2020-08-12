using System;
using System.Threading.Tasks;
using Sample.Consumer.Models;
using Twino.Client.TMQ;
using Twino.Client.TMQ.Annotations;
using Twino.Protocols.TMQ;

namespace Sample.Consumer.Consumers
{
	[PushExceptions("error", 501)]
	public class CRequestHandler: DenemeBase<ModelC, ModelA>
	{
		protected override Task<ModelA> Handle(ModelC request)
		{
			Console.WriteLine("Model C consumed");
			throw new Exception("aaaa");
		}
	}

	public abstract class DenemeBase<T, T1>: ITwinoRequestHandler<T, T1>
	{
		public Task<T1> Handle(T request, TmqMessage rawMessage, TmqClient client)
		{
			throw new Exception("asdasdasdasd");
			return Handle(request);
		}

		public Task<ErrorResponse> OnError(Exception exception, T request, TmqMessage rawMessage, TmqClient client)
		{
			return Task.FromResult(new ErrorResponse());
		}

		protected abstract Task<T1> Handle(T request);
	}
}