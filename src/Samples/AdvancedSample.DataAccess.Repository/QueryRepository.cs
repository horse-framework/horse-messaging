using AdvancedSample.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace AdvancedSample.DataAccess.Repository
{
	internal class QueryRepository<T> : QueryRepositoryBase<T> where T : class, IEntity
	{
		public QueryRepository(DbContext context) : base(context.Set<T>()) { }
	}
}