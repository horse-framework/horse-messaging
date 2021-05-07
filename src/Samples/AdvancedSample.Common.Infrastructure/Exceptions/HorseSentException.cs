using System;
using AdvancedSample.Common.Cqrs.Infrastructure;
using Horse.Messaging.Protocol;

namespace AdvancedSample.Common.Infrastructure.Exceptions
{
	public class HorseSentException : Exception
	{
		public object MessageData { get; }
		protected HorseResult Result { get; init; }

		public HorseSentException(object data, HorseResult result) : base("The message could not be handled.")
		{
			MessageData = data;
			Result = result;
		}
	}

	public sealed class HorseSentException<T> : HorseSentException where T : notnull, IServiceMessage
	{
		public new T MessageData { get; }

		public HorseSentException(T data, HorseResult result) : base(data, result)
		{
			MessageData = data;
		}
	}
}