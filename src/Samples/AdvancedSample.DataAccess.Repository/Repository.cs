using AdvancedSample.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace AdvancedSample.DataAccess.Repository
{
	internal class Repository<T> : RepositoryBase<T> where T : class, IEntity
	{
		public Repository(DbContext context) : base(context) { }
	}
}