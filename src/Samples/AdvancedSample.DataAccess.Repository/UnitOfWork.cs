using System.Threading.Tasks;
using System.Transactions;
using AdvancedSample.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AdvancedSample.DataAccess.Repository
{
	internal sealed class UnitOfWork : IUnitOfWork
	{
		private readonly DbContext _context;

		public UnitOfWork(DbContext context)
		{
			_context = context;
		}

		public IQueryRepository<T> Query<T>() where T : class, IEntity
		{
			return new QueryRepository<T>(_context);
		}

		public ICommandRepository<T> Command<T>() where T : class, IEntity
		{
			return new CommandRepository<T>(_context);
		}

		public Task<int> SaveChangesAsync()
		{
			return _context.SaveChangesAsync();
		}

		public Task<IDbContextTransaction> BeginTransaction()
		{
			return _context.Database.BeginTransactionAsync();
		}

		public void Dispose()
		{
			_context.Dispose();
		}
	}
}