using System;
using System.Data.SqlClient;
using System.Threading;
using log4net;

namespace Negri.Wot.Sql
{
    public abstract class DataAccessBase
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(DataAccessBase));

        protected DataAccessBase(string connectionString)
        {
            ConnectionString = connectionString;
        }

        protected string ConnectionString { get; }

        protected T Get<T>(Func<SqlTransaction, T> getter, int maxTries = 10)
        {
            Exception lastException = null;
            for (var i = 0; i < maxTries; ++i)
            {
                try
                {
                    using (var connection = new SqlConnection(ConnectionString))
                    {
                        connection.Open();
                        using (var t = connection.BeginTransaction("Get"))
                        {
                            T result = getter(t);
                            t.Commit();
                            return result;
                        }
                    }
                }
                catch (SqlException ex)
                {
                    lastException = ex;

                    if (!CanRetry(ex))
                    {
                        Log.Error("Critical DB Exception",ex);
                        throw;
                    }

                    Log.Warn("DB Exception", ex);
                    
                    if (i < maxTries - 1)
                    {
                        var waitSeconds = 1 + i * i * 5;
                        Log.WarnFormat("Esperando {0}s antes de tentar ler do BD novamente.", waitSeconds);
                        Thread.Sleep(TimeSpan.FromSeconds(waitSeconds));
                    }
                }
            }
            if (lastException != null)
            {
                throw lastException;
            }
            throw new InvalidOperationException("Flow error.");
        }

        private static bool CanRetry(SqlException ex)
        {
            
            switch (ex.Number)
            {
                case 2627: // FK Violation
                case 207: // Wrong Column Name
                case 8114: // Wrong Type
                case 8144: // Wrong Number of Args
                case 102: // Syntax
                    return false;
                default:
                    return true;
            }
        }

        protected void Execute(Action<SqlTransaction> action, int maxTries = 10)
        {
            Exception lastException = null;
            for (var i = 0; i < maxTries; ++i)
            {
                try
                {
                    using (var connection = new SqlConnection(ConnectionString))
                    {
                        connection.Open();
                        using (var t = connection.BeginTransaction("Execute"))
                        {
                            try
                            {
                                action(t);
                                t.Commit();
                            }
                            catch (SqlException ex)
                            {
                                Log.Warn("Inner Exception", ex);
                                t.Rollback();
                                throw;
                            }
                            
                            return;
                        }
                    }
                }
                catch (SqlException ex)
                {
                    lastException = ex;

                    if (!CanRetry(ex))
                    {
                        Log.Error("Critical DB Exception", ex);
                        throw;
                    }

                    Log.Warn("DB Exception", ex);

                    if (i < maxTries - 1)
                    {
                        var waitSeconds = 1 + i * i * 5;
                        Log.WarnFormat("Esperando {0}s antes de tentar salvar no BD novamente.", waitSeconds);
                        Thread.Sleep(TimeSpan.FromSeconds(waitSeconds));
                    }
                }
            }
            if (lastException != null)
            {
                throw lastException;
            }
        }

    }
}