using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Direct;
using Horse.Messaging.Client.Direct.Annotations;
using Horse.Messaging.Protocol;
using HostedServiceSample.Common;

namespace HostedServiceSample.Consumer;

[DirectContentType(2)]
[PushExceptions(typeof(SerializedException), typeof(SampleException))]
public class TestRequestModelHandler: IHorseRequestHandler<TestRequestModel, TestResponseModel>
{
	public Task<TestResponseModel> Handle(TestRequestModel request, HorseMessage rawMessage, HorseClient client)
	{
		var source = new List<TestSource>
		{
			new() { Value = "Val1" },
			null
		};
		var response = new TestResponseModel
		{
			Items = source.Select(m => new TestResponseModelItem { Value = m.Value }).AsEnumerable()
		};
		return Task.FromResult(response);
	}

	public Task<ErrorResponse> OnError(Exception exception, TestRequestModel request, HorseMessage rawMessage, HorseClient client)
	{
		return exception switch
		{
			SampleException sampleException => Task.FromResult(new ErrorResponse { Reason = "Reason is no matter.", ResultCode = HorseResultCode.Failed }),
			_                               => throw new SampleException()
		};
	}
}

internal class TestSource
{
	public string Value { get; set; }
}

internal class TestSourceMap
{
	public TestSource TestSource { get; set; }
}