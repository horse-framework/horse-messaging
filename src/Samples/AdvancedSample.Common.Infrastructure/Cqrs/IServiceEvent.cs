using System;

namespace AdvancedSample.Common.Cqrs.Infrastructure
{
	public interface IServiceEvent : IServiceMessage
	{
		public Guid EventId { get; }
	}
}