using System;

namespace Negri.Wot.Threading
{
    /// <summary>
    ///     Espera um tempo constante entre as tentativas
    /// </summary>
    public class FixedRetryPolicy : RetryPolicy
    {
        private readonly TimeSpan _interval;

        /// <summary>
        ///     Initializes a new instance of the <see cref="FixedRetryPolicy" /> class.
        /// </summary>
        /// <param name="interval">The interval.</param>
        public FixedRetryPolicy(TimeSpan interval)
        {
            _interval = interval;
        }

        /// <summary>
        ///     Devolve o tempo de espera
        /// </summary>
        /// <param name="currentRetryNumber">O número da tentativa, a primeira vez essa função é chamada com o valor 1</param>
        /// <returns></returns>
        protected override TimeSpan GetWaitTime(int currentRetryNumber)
        {
            return _interval;
        }
    }
}