using System;

namespace AdvancedSample.Core.Domain
{
	public interface ISoftDeletableEntity : IEntity
	{
		public bool IsDeleted { get; set; }
		public DateTime DeletedAt { get; set; }
	}
}