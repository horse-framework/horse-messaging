using System;

namespace AdvancedSample.Common.Cqrs.Infrastructure
{
	public abstract class ServiceEvent : IServiceEvent
	{
		public Guid EventId { get; set; }
	}
}