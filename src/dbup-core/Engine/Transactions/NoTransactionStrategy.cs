using System;
using System.Collections.Generic;
using System.Data;
using DbUp.Engine.Output;

namespace DbUp.Engine.Transactions
{
    internal class NoTransactionStrategy : ITransactionStrategy
    {
        private IDbConnection connection;

        public void Execute(Action<Func<IDbCommand>> action)
        {
            action(()=>connection.CreateCommand());
        }

        public T Execute<T>(Func<Func<IDbCommand>, T> actionWithResult)
        {
            return actionWithResult(() =>
            {
                var command = connection.CreateCommand();
                command.CommandTimeout = (int)TimeSpan.FromMilliseconds(30).TotalSeconds;
                return command;
            });
        }

        public void Initialise(IDbConnection dbConnection, IUpgradeLog upgradeLog)
        {
            connection = dbConnection;
        }

        public void Dispose() { }
    }
}