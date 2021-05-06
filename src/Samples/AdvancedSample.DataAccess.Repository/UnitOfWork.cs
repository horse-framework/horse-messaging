using System;
using System.Threading.Tasks;
using System.Transactions;
using AdvancedSample.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace AdvancedSample.DataAccess.Repository
{
	public interface IUnitOfWork : IDisposable
	{
		public IQueryRepository<T> Query<T>() where T : class, IEntity;
		public ICommandRepository<T> Command<T>() where T : class, IEntity;
		public Task<int> SaveChangesAsync();
		public void BeginTransaction();
		public void CommitTransaction();
	}

	public sealed class UnitOfWork : IUnitOfWork
	{
		private readonly DbContext _context;
		private TransactionScope _scope;

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

		public void BeginTransaction()
		{
			_scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
		}

		public void CommitTransaction()
		{
			_scope?.Complete();
		}

		public void Dispose()
		{
			_scope?.Dispose();
			_context.Dispose();
		}
	}
}