using System;

namespace AdvancedSample.Common.Cqrs.Infrastructure
{
	public interface IServiceQuery : IServiceMessage
	{
		public Guid QueryId { get; }
	}
}