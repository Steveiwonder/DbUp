using System;
using System.Collections.Generic;
using System.Data;
using DbUp.Engine.Output;

namespace DbUp.Engine.Transactions
{
    /// <summary>
    /// Manages Sql Database Connections
    /// </summary>
    /// <seealso cref="DbUp.Engine.Transactions.IConnectionManager" />
    public abstract class DatabaseConnectionManager : IConnectionManager
    {
        readonly IConnectionFactory connectionFactory;
        ITransactionStrategy transactionStrategy;
        readonly Dictionary<TransactionMode, Func<ITransactionStrategy>> transactionStrategyFactory;
        IDbConnection upgradeConnection;
        IConnectionFactory connectionFactoryOverride;

        /// <summary>
        /// Manages Database Connections
        /// </summary>
        protected DatabaseConnectionManager(Func<IUpgradeLog, IDbConnection> connectionFactory) : this(new DelegateConnectionFactory(connectionFactory))
        {
        }

        /// <summary>
        /// Manages Database Connections
        /// </summary>
        protected DatabaseConnectionManager(IConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
            transactionStrategyFactory = new Dictionary<TransactionMode, Func<ITransactionStrategy>>
            {
                {TransactionMode.NoTransaction, ()=>new NoTransactionStrategy()},
                {TransactionMode.SingleTransaction, ()=>new SingleTransactionStrategy()},
                {TransactionMode.TransactionPerScript, ()=>new TransactionPerScriptStrategy()}
            };
        }

        /// <summary>
        /// Tells the connection manager is starting
        /// </summary>
        public IDisposable OperationStarting(IUpgradeLog upgradeLog, List<SqlScript> executedScripts, TransactionMode transactionMode)
        {
            upgradeConnection = CreateConnection(upgradeLog);
            if (upgradeConnection.State == ConnectionState.Closed)
                upgradeConnection.Open();


            transactionStrategy = transactionStrategyFactory[transactionMode]();
            transactionStrategy.Initialise(upgradeConnection, upgradeLog);

            return new DelegateDisposable(() =>
            {
                transactionStrategy.Dispose();
                upgradeConnection.Dispose();
                transactionStrategy = null;
                upgradeConnection = null;
            });
        }

        /// <summary>
        /// Tries to connect to the database.
        /// </summary>
        public bool TryConnect(IUpgradeLog upgradeLog, out string errorMessage)
        {
            try
            {
                errorMessage = "";
                upgradeConnection = CreateConnection(upgradeLog);
                if (upgradeConnection.State == ConnectionState.Closed)
                    upgradeConnection.Open();
                var strategy = transactionStrategyFactory[TransactionMode.NoTransaction]();
                strategy.Initialise(upgradeConnection, upgradeLog);
                strategy.Execute(dbCommandFactory =>
                {
                    using (var command = dbCommandFactory())
                    {
                        command.CommandText = "select 1";
                        command.ExecuteScalar();
                    }
                });
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Executes an action using the specified transaction mode
        /// </summary>
        /// <param name="action">The action to execute</param>
        public void ExecuteCommandsWithManagedConnection(Action<Func<IDbCommand>> action)
        {
            transactionStrategy.Execute(action);
        }

        /// <summary>
        /// Executes an action which has a result using the specified transaction mode
        /// </summary>
        /// <typeparam name="T">The result type</typeparam>
        /// <param name="actionWithResult">The action to execute</param>
        /// <returns>
        /// The result of the command
        /// </returns>
        public T ExecuteCommandsWithManagedConnection<T>(Func<Func<IDbCommand>, T> actionWithResult)
        {
            return transactionStrategy.Execute(actionWithResult);
        }

        /// <summary>
        /// The transaction strategy that DbUp should use
        /// </summary>

        /// <summary>
        /// Specifies whether the db script output should be logged
        /// </summary>
        public bool IsScriptOutputLogged { get; set; }

        /// <summary>
        /// Splits a script into commands, for example SQL Server separates command by the GO statement
        /// </summary>
        /// <param name="scriptContents">The script</param>
        /// <returns>A list of SQL Commands</returns>
        public abstract IEnumerable<string> SplitScriptIntoCommands(string scriptContents);

        public IDisposable OverrideFactoryForTest(IConnectionFactory connectionFactory)
        {
            connectionFactoryOverride = connectionFactory;
            return new DelegateDisposable(() => connectionFactoryOverride = null);
        }

        IDbConnection CreateConnection(IUpgradeLog upgradeLog)
        {
            return (connectionFactoryOverride ?? connectionFactory).CreateConnection(upgradeLog, this);
        }
    }
}
