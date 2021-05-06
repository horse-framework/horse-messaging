using System;
using AdvancedSample.Common.Cqrs.Infrastructure;
using Horse.Messaging.Protocol;

namespace AdvancedSample.Common.Infrastructure.Exceptions
{
	public sealed class HorsePublishException<T> : Exception where T : notnull, IServiceMessage
	{
		public T MessageData { get; }
		public HorseResult Result { get; }

		public HorsePublishException(T data, HorseResult result) : base("The message could not be published to router.")
		{
			MessageData = data;
			Result = result;
		}
	}
}