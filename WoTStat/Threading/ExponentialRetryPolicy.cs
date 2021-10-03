using System;

namespace Negri.Wot.Threading
{
    /// <summary>
    ///     Espera um tempo progressivamente, e exponencialmente, maior a cada tentativa
    /// </summary>
    public class ExponentialRetryPolicy : RetryPolicy
    {
        private static readonly Random Random = new();

        private readonly TimeSpan _maxWait;
        private readonly TimeSpan _minWait;
        private readonly TimeSpan _progress;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ExponentialRetryPolicy" /> class.
        /// </summary>
        /// <param name="progress">O progresso a cada tentativa</param>
        /// <param name="minWait">O intervalo mínimo, opcional</param>
        /// <param name="maxWait">O intervalo máximo, opcional</param>
        public ExponentialRetryPolicy(TimeSpan progress, TimeSpan minWait = default,
            TimeSpan maxWait = default)
        {
            _progress = progress;

            if (minWait == default) minWait = TimeSpan.Zero;
            _minWait = minWait;

            if (maxWait == default) maxWait = TimeSpan.MaxValue;
            _maxWait = maxWait;
        }

        /// <summary>
        ///     Devolve o tempo de espera
        /// </summary>
        /// <param name="currentRetryNumber">O número da tentativa, a primeira vez essa função é chamada com o valor 1</param>
        /// <remarks>
        ///     O tempo de incremento não é determinístico. A formula é a mesma usda pela classe
        ///     Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling.RetryPolicy
        /// </remarks>
        protected override TimeSpan GetWaitTime(int currentRetryNumber)
        {
            var num = (Math.Pow(2.0, currentRetryNumber - 1) - 1.0) *
                      Random.Next((int) (_progress.TotalMilliseconds * 0.8),
                          (int) (_progress.TotalMilliseconds * 1.2));

            var num2 = _minWait.TotalMilliseconds + num;
            num2 = Math.Min(num2, _maxWait.TotalMilliseconds);

            return TimeSpan.FromMilliseconds(num2);
        }
    }
}