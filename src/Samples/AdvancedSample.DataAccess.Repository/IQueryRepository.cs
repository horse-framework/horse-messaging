using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AdvancedSample.Core.Domain;

namespace AdvancedSample.DataAccess.Repository
{
	public interface IQueryRepository<T> where T : IEntity
	{
		public IQueryable<T> GetAll();
		public IQueryable<T> FindAll(Expression<Func<T, bool>> predicate);
		public ValueTask<T> Get(int id);
		public Task<T> Find(Expression<Func<T, bool>> predicate);
	}
}