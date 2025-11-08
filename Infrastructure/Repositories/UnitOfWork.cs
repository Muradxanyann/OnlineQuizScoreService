using System.Data;
using Domain.Interfaces;

namespace Infrastructure.Repositories;

 public class UnitOfWork : IUnitOfWork
    {
        private IDbConnection _connection;
        private IDbTransaction? _transaction;
        private bool _disposed;

        private IUserResultRepository? _userResultRepository;

        public UnitOfWork(DapperDbContext context)
        {
            _connection = context.CreateConnection();
            _connection.Open();
        }

        public IUserResultRepository UserResults =>
            _userResultRepository ??= new DapperUserResultRepository(_connection, _transaction);

        public void BeginTransaction()
        {
            if (_transaction != null)
                throw new InvalidOperationException("Транзакция уже начата.");

            _transaction = _connection.BeginTransaction();
            UpdateTransactionForRepositories();
        }

        public async Task CommitAsync()
        {
            try
            {
                _transaction?.Commit();
            }
            catch
            {
                _transaction?.Rollback();
                throw;
            }
            finally
            {
                _transaction?.Dispose();
                _transaction = null;
                UpdateTransactionForRepositories();
            }
        }

        public void Rollback()
        {
            try
            {
                _transaction?.Rollback();
            }
            finally
            {
                _transaction?.Dispose();
                _transaction = null;
                UpdateTransactionForRepositories();
            }
        }

        // Передает (или обнуляет) транзакцию во все репозитории
        private void UpdateTransactionForRepositories()
        {
            (_userResultRepository as DapperUserResultRepository)?.SetTransaction(_transaction);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _transaction?.Dispose();
                    _connection?.Dispose();
                }
                _disposed = true;
            }
        }
    }