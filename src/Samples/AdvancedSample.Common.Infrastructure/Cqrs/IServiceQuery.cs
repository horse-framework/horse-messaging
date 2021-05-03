using System;

namespace AdvancedSample.Common.Cqrs.Infrastructure
{
	public interface IServiceQuery
	{
		public Guid QueryId { get; }
	}
}