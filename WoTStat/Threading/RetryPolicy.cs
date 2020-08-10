using System;
using System.Threading.Tasks;

namespace Negri.Wot.Threading
{
    /// <summary>
    ///     Classe para implementar retry automático de operações que podem sofrer de erros transientes
    /// </summary>
    public abstract class RetryPolicy
    {
        /// <summary>
        ///     Se a primeira tentativa após falha deve ser imediatamente executada
        /// </summary>
        public bool FastFirstRetry { get; set; } = true;

        /// <summary>
        ///     O numero máximo de tentativas padrão
        /// </summary>
        public int DefaultMaxTries { get; set; } = 5;

        /// <summary>
        ///     Retorna a politica padrão, com os intervalos padrão de espera (Exponencial com progresso de 100ms)
        /// </summary>
        public static RetryPolicy Default => GetExponentialRetryPolicy(TimeSpan.FromMilliseconds(100));

        /// <summary>
        ///     Obtém uma politica de esperar um tempo constante entre as tentativas
        /// </summary>
        /// <param name="wait">Tempo a esperar entre as tentativas</param>
        public static RetryPolicy GetFixedRetryPolicy(TimeSpan wait)
        {
            return new FixedRetryPolicy(wait);
        }

        /// <summary>
        ///     Obtém uma politica de intervalos de tentativas esperando exponencialmente mais.
        /// </summary>
        public static RetryPolicy GetExponentialRetryPolicy(TimeSpan progress, TimeSpan minWait = default,
            TimeSpan maxWait = default)
        {
            return new ExponentialRetryPolicy(progress, minWait, maxWait);
        }

        /// <summary>
        ///     Executa com a politica padrão (Exponencial)
        /// </summary>
        /// <param name="action">A ação a ser executada</param>
        /// <param name="maxTries">Numero de tentativas, opcional, o padrão são 10 que leva a cerca de 1,5 minutos de espera</param>
        public static void ExecuteDefault(Action action, int? maxTries = null)
        {
            Default.ExecuteAction(action, maxTries ?? 10);
        }

        /// <summary>
        ///     Executa com a politica padrão (Exponencial)
        /// </summary>
        /// <param name="action">A ação a ser executada</param>
        /// <param name="maxTries">Numero de tentativas, opcional, o padrão são 10 que leva a cerca de 1,5 minutos de espera</param>
        public static T ExecuteDefault<T>(Func<T> action, int? maxTries = null)
        {
            return Default.ExecuteAction(action, maxTries ?? 10);
        }

        /// <summary>
        ///     Executa uma função com a possibilidade de tentar novamente
        /// </summary>
        /// <param name="action">A ação a ser executada</param>
        /// <param name="maxTries">Numero total de tentativas a serem feitas</param>
        /// <param name="beforeWaitAction">Função, opcional, que é chamada imediatamente antes de esperar</param>
        /// <param name="isTransientError">
        ///     Função para determinar se uma exceção é transiente ou não. Se não informado, toda
        ///     exceção é considerada transiente.
        /// </param>
        public void ExecuteAction(Action action, int? maxTries = null,
            Action<int, TimeSpan, Exception> beforeWaitAction = null, Func<Exception, bool> isTransientError = null)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            ExecuteAction<object>(() =>
            {
                action();
                return null;
            }, maxTries, beforeWaitAction, isTransientError);
        }

        /// <summary>
        ///     Executa uma função com a possibilidade de tentar novamente
        /// </summary>
        /// <typeparam name="T">O tipo do retorno da função</typeparam>
        /// <param name="func">A função</param>
        /// <param name="maxTries">Numero total de tentativas a serem feitas</param>
        /// <param name="beforeWaitAction">Função, opcional, que é chamada imediatamente antes de esperar</param>
        /// <param name="isTransientError">
        ///     Função para determinar se uma exceção é transiente ou não. Se não informado, toda
        ///     exceção é considerada transiente.
        /// </param>
        public T ExecuteAction<T>(Func<T> func, int? maxTries = null,
            Action<int, TimeSpan, Exception> beforeWaitAction = null, Func<Exception, bool> isTransientError = null)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));

            var effectiveMaxTries = maxTries ?? DefaultMaxTries;
            var num = 0;
            while (true)
                try
                {
                    return func();
                }
                catch (Exception ex)
                {
                    if (isTransientError != null)
                    {
                        var isTransient = isTransientError(ex);
                        if (!isTransient) throw;
                    }

                    ++num;
                    if (num >= effectiveMaxTries) throw;

                    TimeSpan wait;
                    if (num == 1 && FastFirstRetry)
                        wait = TimeSpan.Zero;
                    else
                        wait = GetWaitTime(num);

                    beforeWaitAction?.Invoke(num, wait, ex);

                    Task.Delay(wait).Wait(wait);
                }
        }

        /// <summary>
        ///     Devolve o tempo de espera
        /// </summary>
        /// <param name="currentRetryNumber">O número da tentativa, a primeira vez essa função é chamada com o valor 1</param>
        /// <returns></returns>
        protected abstract TimeSpan GetWaitTime(int currentRetryNumber);
    }
}