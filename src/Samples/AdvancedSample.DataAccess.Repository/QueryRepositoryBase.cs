using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AdvancedSample.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AdvancedSample.DataAccess.Repository
{
	internal abstract class QueryRepositoryBase<T> : IQueryRepository<T>
		where T : class, IEntity
	{
		private readonly DbSet<T> _table;

		protected QueryRepositoryBase(DbSet<T> table)
		{
			_table = table;
		}

		public IQueryable<T> Table => _table;

		public IQueryable<T> GetAll()
		{
			return _table;
		}

		public IQueryable<T> FindAll(Expression<Func<T, bool>> predicate)
		{
			return _table.Where(predicate);
		}

		public ValueTask<T> Get(int id)
		{
			return _table.FindAsync(id);
		}

		public Task<T> Find(Expression<Func<T, bool>> predicate)
		{
			return _table.FirstAsync(predicate);
		}
	}
}