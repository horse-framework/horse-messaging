using System;

namespace AdvancedSample.Core.Domain
{
	public interface IAuditableEntity : IEntity
	{
		public DateTime CreatedAt { get; set; }
		public DateTime UpdatedAt { get; set; }
	}
}