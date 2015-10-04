using System;
using System.Data;
using Shuttle.Core.Infrastructure;

namespace Shuttle.Core.Data
{
    public class DatabaseConnection : IDatabaseConnection
    {
        private readonly DataSource _dataSource;
        private readonly IDbCommandFactory _dbCommandFactory;
        private readonly IDatabaseConnectionCache _databaseConnectionCache;
        private readonly ILog _log;
        private readonly bool _ownedConnection;
        private readonly bool _ownedTransaction;

        private bool disposed;

        public DatabaseConnection(DataSource dataSource, IDbConnection connection, IDbCommandFactory dbCommandFactory, 
            IDatabaseConnectionCache databaseConnectionCache, bool ownedConnection = true, IDbTransaction currentTransaction = null)
        {
            Guard.AgainstNull(dataSource, "dataSource");
            Guard.AgainstNull(connection, "connection");
            Guard.AgainstNull(dbCommandFactory, "dbCommandFactory");
            Guard.AgainstNull(databaseConnectionCache, "databaseConnectionCache");

            _dataSource = dataSource;
            _dbCommandFactory = dbCommandFactory;
            _databaseConnectionCache = databaseConnectionCache;

            Connection = connection;
            _ownedConnection = ownedConnection;
            Transaction = currentTransaction;
            _ownedTransaction = currentTransaction == null;

            _log = Log.For(this);

            _log.Verbose(string.Format(DataResources.DbConnectionCreated, dataSource.Name));

            try
            {
                if (connection.State == ConnectionState.Closed)
                {
                    Connection.Open();
                    _log.Verbose(string.Format(DataResources.DbConnectionOpened, dataSource.Name));
                }
                else
                {
                    _log.Verbose(string.Format(DataResources.DbConnectionAlreadyOpen, dataSource.Name));
                }
            }
            catch (Exception ex)
            {
                _log.Error(string.Format(DataResources.DbConnectionOpenException, dataSource.Name, ex.Message));

                throw;
            }

            databaseConnectionCache.Add(dataSource, this);
        }

        public IDbCommand CreateCommandToExecute(IQuery query)
        {
            var command = _dbCommandFactory.CreateCommandUsing(_dataSource, Connection, query);
            command.Transaction = Transaction;
            return command;
        }

        public bool HasTransaction
        {
            get { return Transaction != null; }
        }

        public IDbTransaction Transaction { get; private set; }
        public IDbConnection Connection { get; private set; }

        public IDatabaseConnection BeginTransaction()
        {
            if (!HasTransaction && System.Transactions.Transaction.Current == null)
            {
                Transaction = Connection.BeginTransaction();
            }

            return this;
        }

        public void CommitTransaction()
        {
            if (!HasTransaction || !_ownedTransaction)
            {
                return;
            }

            Transaction.Commit();
            Transaction = null;
        }

        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                if (HasTransaction && _ownedTransaction)
                {
                    Transaction.Rollback();
                }
                if (_ownedConnection)
                {
                    Connection.Dispose();
                }
                _databaseConnectionCache.Remove(_dataSource);
            }

            Connection = null;
            disposed = true;
        }
    }
}