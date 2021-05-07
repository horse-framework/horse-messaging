using AdvancedSample.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace AdvancedSample.DataAccess.Repository
{
	internal class CommandRepository<T> : CommandRepositoryBase<T> where T : class, IEntity
	{
		public CommandRepository(DbContext context) : base(context) { }
	}
}