using System;
using AdvancedSample.Common.Cqrs.Infrastructure;

namespace AdvancedSample.Common.Infrastructure.Exceptions
{
	public sealed class SaveToOutboxFailed : Exception
	{
		public IServiceEvent Event { get; }

		public SaveToOutboxFailed(IServiceEvent @event) : base($"$The event could not be saved to outbox.")
		{
			Event = @event;
		}
	}
}