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
                    Log.Warn(ex);
                    if (ex.Number == 2627)
                    {
                        // FK errada não adianta tentar
                        throw;
                    }

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
            throw new InvalidOperationException("Erro de Fluxo.");
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
                    Log.Warn("Outer Exception", ex);
                    lastException = ex;                    
                    if (ex.Number == 2627)
                    {
                        // FK errada não adianta tentar
                        throw;
                    }

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