using System;
using Horse.Messaging.Client.Queues;

namespace HostedServiceSample.Common;

public class SampleException: Exception { }

public class SerializedException: ITransportableException
{
	public string Message { get; set; }

	public void Initialize(ExceptionContext context)
	{
		Message = context.Exception.Message;
	}
}