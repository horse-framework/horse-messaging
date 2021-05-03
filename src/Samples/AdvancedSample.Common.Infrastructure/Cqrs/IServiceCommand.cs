using System;

namespace AdvancedSample.Common.Cqrs.Infrastructure
{
	public interface IServiceCommand
	{
		public Guid CommandId { get; }
	}
}