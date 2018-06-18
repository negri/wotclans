using System;
using System.Diagnostics.CodeAnalysis;

namespace Negri.Wot.Mathematic
{
    /// <summary>
    ///     Classe com soluções para resolver equações
    /// </summary>
    public static class EquationSolver
    {
        /// <summary>
        ///     Declara o delegate da função
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        public delegate double Function(double x);


        /// <summary>
        ///     Métodos de resolução de Equações
        /// </summary>
        public enum Method
        {
            /// <summary>
            ///     Método das secantes
            /// </summary>
            Secant,

            /// <summary>
            ///     Bissecção em funções monótonas
            /// </summary>
            Bisection,

            /// <summary>
            ///     Bissecção em funções não lineares, por trechos
            /// </summary>
            BisectionLowerSolution,

            /// <summary>
            ///     Brent, o método recomendado.
            /// </summary>
            Brent
        }


        /// <summary>
        ///     Razão pela qual foi encerrada a busca
        /// </summary>
        [Flags]
        public enum StopReasons
        {
            /// <summary>
            ///     Nenhuma razão para parar
            /// </summary>
            None = 0,

            /// <summary>
            ///     The solution was found
            /// </summary>
            SolutionFound = 1,

            /// <summary>
            ///     The maximum numbr of steps were reached
            /// </summary>
            MaximumNumberOfSteps = 2,

            /// <summary>
            ///     Convergence on X
            /// </summary>
            DeltaX = 4,

            /// <summary>
            ///     Convergence on Y
            /// </summary>
            DeltaY = 8,

            /// <summary>
            ///     Timeout
            /// </summary>
            Timeout = 16,

            /// <summary>
            ///     Chute de máximo e minimo ruim, que provavelmente não inclui o zero da função
            /// </summary>
            BadMinMaxGuess = 32
        }

        /// <summary>
        ///     Solves by the method.
        /// </summary>
        /// <param name="method">The method.</param>
        /// <param name="minX">The min X.</param>
        /// <param name="maxX">The max X.</param>
        /// <param name="f">The function</param>
        /// <param name="stopConditions">The stop conditions.</param>
        /// <returns>Hopefuly, a solution</returns>
        public static Solution Solve(Method method, double minX, double maxX, Function f, StopConditions stopConditions)
        {
            switch (method)
            {
                case Method.Secant:
                    return Secant(minX, maxX, f, stopConditions);
                case Method.Bisection:
                    return Bisection(minX, maxX, f, stopConditions);
                case Method.BisectionLowerSolution:
                    return BisectionLowerSolution(minX, maxX, f, stopConditions, 20);
                case Method.Brent:
                    return BrentMethod(minX, maxX, f, stopConditions);
                default:
                    throw new ArgumentOutOfRangeException(nameof(method));
            }
        }

        /// <summary>
        ///     Solves function (Find it´s zero
        /// </summary>
        /// <param name="minX">The min X.</param>
        /// <param name="maxX">The max X.</param>
        /// <param name="f">The functions</param>
        /// <returns>Hopefuly, a solution</returns>
        /// <remarks>It uses the Brent method, with a 10s timeout if release code, or 5min on debug, or 100 iterations.</remarks>
        public static Solution Solve(double minX, double maxX, Function f)
        {
            #if DEBUG
            var timeout = TimeSpan.FromMinutes(5);
            #else
            TimeSpan timeout = TimeSpan.FromSeconds(10);
            #endif

            return Solve(Method.Brent, minX, maxX, f, new StopConditions(100, null, null, timeout));
        }

        private static Solution Secant(double minX, double maxX, Function f, StopConditions stopConditions)
        {
            var utcStart = DateTime.UtcNow;

            var p0 = f(minX);
            var p1 = f(maxX);
            var d = f(p1) * (p1 - p0) / (f(p1) - f(p0));
            var p2 = p1 - d;

            int i;
            StopReasons stopReasons;
            for (i = 0; !stopConditions.ShouldStop(i, d, f(p2), utcStart, out stopReasons); i++)
            {
                p0 = p1;
                p1 = p2;
                d = f(p1) * (p1 - p0) / (f(p1) - f(p0));
                p2 = p1 - d;
            }

            if ((stopReasons & StopReasons.SolutionFound) == StopReasons.SolutionFound)
            {
                return new Solution(true, p2, i, stopReasons);
            }

            return new Solution(false, p2, i, stopReasons);
        }


        [SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
        private static Solution Bisection(double minX, double maxX, Function f, StopConditions stopConditions)
        {
            var utcStart = DateTime.UtcNow;
            var x0 = minX;
            var x1 = maxX;

            var y0 = f(x0);
            var y1 = f(x1);

            // verifica se achou raiz
            if (y0 == 0)
            {
                return new Solution(true, x0, 0, StopReasons.SolutionFound);
            }

            if (y1 == 0)
            {
                return new Solution(true, x1, 0, StopReasons.SolutionFound);
            }

            // retorna false caso não exista raiz no intervalo (sinais iguais)
            if (y0 < 0 ? y1 < 0 : y1 > 0)
            {
                return Math.Abs(y0) < Math.Abs(y1)
                    ? new Solution(false, x0, 0, StopReasons.DeltaX)
                    : new Solution(false, x1, 0, StopReasons.DeltaX);
            }

            // iterações da bissecção
            var i = 0;
            do
            {
                var xi = (x0 + x1) / 2.0;

                //double y0 = f(x0); // tirei do loop
                var yi = f(xi);

                // se sinal invertido
                if (y0 < 0 ? yi >= 0 : yi < 0)
                {
                    x1 = xi;
                    // y0 = y0; // continua o mesmo
                }
                else
                {
                    x0 = xi;
                    y0 = yi;
                }

                if (stopConditions.ShouldStop(i, Math.Abs(x1 - x0), yi, utcStart, out var stopReasons))
                {
                    return new Solution((stopReasons & StopReasons.SolutionFound) == StopReasons.SolutionFound,
                        x1, i, stopReasons);
                }

                ++i;
            } while (true);
        }

        /// <summary>
        ///     Bisections the lower solution.
        /// </summary>
        /// <param name="minX">The min X.</param>
        /// <param name="maxX">The max X.</param>
        /// <param name="f">The f.</param>
        /// <param name="stopConditions">The stop conditions.</param>
        /// <param name="numberOfFractions">The number of fractions.</param>
        /// <returns></returns>
        public static Solution BisectionLowerSolution(double minX, double maxX, Function f,
            StopConditions stopConditions, int numberOfFractions)
        {
            var x0 = minX;
            var x1 = maxX;

            // tamanho do espaço dividido
            var fraction = (x1 - x0) / numberOfFractions;

            var a0 = x0;
            var b0 = f(a0);

            for (var i = 0; i < numberOfFractions; i++)
            {
                // definição dos limites
                var a1 = a0 + fraction;
                var b1 = f(a1);

                // chama bissecção no primeiro que tiver sinal invertido (presença de raiz)
                if (b0 < 0 ? b1 > 0 : b1 < 0)
                {
                    return Bisection(a0, a1, f, stopConditions);
                }

                // proxima iteração
                a0 = a1;
                b0 = b1;
            }

            return new Solution(false, x1, numberOfFractions, StopReasons.MaximumNumberOfSteps);
        }

        [SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
        private static Solution BrentMethod(double minX, double maxX, Function f, StopConditions stopConditions)
        {
            var utcStart = DateTime.UtcNow;
            var a = minX;
            var b = maxX;

            var iterations = 0;

            //input a, b, and a pointer to a subroutine for f 

            //calculate f(a) 
            var fa = f(a);

            //calculate f(b) 
            var fb = f(b);

            //if f(a) f(b) >= 0 then error-exit end if 
            if (fa * fb >= 0)
            {
                return new Solution(false, 0, 0, StopReasons.BadMinMaxGuess);
            }

            //if |f(a)| < |f(b)| then swap (a,b) end if 
            if (Math.Abs(fa) < Math.Abs(fb))
            {
                Swap(ref a, ref b);
                Swap(ref fa, ref fb);
            }

            //c := a 
            var c = a;
            var fc = fa;
            double d = 0;

            //set mflag 
            var mflag = true;

            //repeat until f(b) = 0 or |b - a| is small enough (convergence) 
            const double delta = 1e-10;
            do
            {
                iterations++;

                //if f(a) != f(c) and f(b) != f(c) then 
                // fa = f(a);
                // fb = f(b);
                // fc = f(c);

                double s;

                if (fa != fc && fb != fc)
                {
                    // (inverse quadratic interpolation) 
                    s = a * fb * fc / ((fa - fb) * (fa - fc));
                    s += b * fa * fc / ((fb - fa) * (fb - fc));
                    s += c * fa * fb / ((fc - fa) * (fc - fb));
                }
                else
                {
                    // (secant rule) 
                    s = b - fb * (b - a) / (fb - fa);
                }

                //if s is not between (3a + b)/4 and b or (mflag is set and |s-b| >= |b-c| / 2) or (mflag is cleared and |s-b| >= |c-d| / 2) then
                if (!Between(s, (3 * a + b) / 4.0, b) ||
                    mflag && Math.Abs(s - b) >= Math.Abs(b - c) / 2.0 ||
                    !mflag && Math.Abs(s - b) >= Math.Abs(c - d) / 2.0)
                {
                    s = (a + b) / 2;

                    //set mflag 
                    mflag = true;
                }
                else
                {
                    //if (mflag is set and |b-c| < |delta|) or (mflag is cleared and |c-d| < |delta|) then
                    if (mflag && Math.Abs(b - c) < delta ||
                        !mflag && Math.Abs(c - d) < delta)
                    {
                        // formula...
                        s = (a + b) / 2;

                        //set mflag 
                        mflag = true;
                    }
                    else
                    {
                        //clear mflag 
                        mflag = false;
                    }

                    //end if 
                    //end if 
                }

                //calculate f(s) 
                var fs = f(s);

                //d := c 
                d = c;

                //c := b 
                c = b;
                fc = fb; //*

                //if f(a) f(s) < 0 then b := s else a := s end if 
                if (fa * fs < 0)
                {
                    b = s;
                    fb = fs;
                }
                else
                {
                    a = s;
                    fa = fs;
                }

                //if |f(a)| < |f(b)| then swap (a,b) end if 
                if (Math.Abs(fa) < Math.Abs(fb))
                {
                    Swap(ref a, ref b);
                    Swap(ref fa, ref fb);
                }
                //end repeat  

                if (stopConditions.ShouldStop(iterations, Math.Abs(b - a), fb, utcStart, out var stopReasons))
                {
                    return new Solution((stopReasons & StopReasons.SolutionFound) == StopReasons.SolutionFound,
                        b, iterations, stopReasons);
                }
            } while (true);
        }

        private static bool Between(double value, double lim1, double lim2)
        {
            if (lim1 < value && value < lim2)
            {
                return true;
            }

            if (lim2 < value && value < lim1)
            {
                return true;
            }

            return false;
        }

        private static void Swap(ref double a, ref double b)
        {
            var aux = a;
            a = b;
            b = aux;
        }

        /// <summary>
        ///     Solução das equações algébricas
        /// </summary>
        public struct Solution
        {
            /// <summary>
            ///     Initializes a new instance of the <see cref="Solution" /> struct.
            /// </summary>
            /// <param name="success">if set to <c>true</c> [Success].</param>
            /// <param name="value">The value.</param>
            /// <param name="iterations">The iterations.</param>
            /// <param name="stopReasons">The stop reasons.</param>
            public Solution(bool success, double value, int iterations, StopReasons stopReasons) : this()
            {
                Success = success;
                Result = value;
                Iterations = iterations;
                StopReasons = stopReasons;
            }


            /// <summary>
            ///     Gets the stop reasons.
            /// </summary>
            /// <value>The stop reasons.</value>
            public StopReasons StopReasons { get; }

            /// <summary>
            ///     Gets a value indicating whether this <see cref="Solution" /> is Success.
            /// </summary>
            /// <value><c>true</c> if Success; otherwise, <c>false</c>.</value>
            public bool Success { get; }

            /// <summary>
            ///     Gets the result.
            /// </summary>
            /// <value>The result.</value>
            public double Result { get; }

            /// <summary>
            ///     Gets or sets the iterations.
            /// </summary>
            /// <value>The iterations.</value>
            public int Iterations { get; }
        }

        /// <summary>
        ///     Condições de parada das buscas
        /// </summary>
        public struct StopConditions
        {
            /// <summary>
            ///     Initializes a new instance of the <see cref="StopConditions" /> struct.
            /// </summary>
            /// <param name="maximumNumberOfSteps">The maximum number of steps.</param>
            /// <param name="deltaX">The delta X.</param>
            /// <param name="deltaY">The delta Y.</param>
            /// <param name="timeout">The timeout.</param>
            public StopConditions(int? maximumNumberOfSteps, double? deltaX, double? deltaY, TimeSpan? timeout)
            {
                MaximumNumberOfSteps = maximumNumberOfSteps;
                DeltaX = deltaX;
                DeltaY = deltaY;
                Timeout = timeout;
            }

            /// <summary>
            ///     Initializes a new instance of the <see cref="StopConditions" /> struct.
            /// </summary>
            /// <param name="maximumNumberOfSteps">The maximum number of steps.</param>
            public StopConditions(int maximumNumberOfSteps) : this(maximumNumberOfSteps, null, null, null)
            {
            }

            /// <summary>
            ///     Initializes a new instance of the <see cref="StopConditions" /> struct.
            /// </summary>
            /// <param name="deltaX">The delta X.</param>
            public StopConditions(double deltaX) : this(null, deltaX, null, null)
            {
            }

            /// <summary>
            ///     Initializes a new instance of the <see cref="StopConditions" /> struct.
            /// </summary>
            /// <param name="timeout">The timeout.</param>
            public StopConditions(TimeSpan timeout) : this(null, null, null, timeout)
            {
            }

            /// <summary>
            ///     Gets the timeout.
            /// </summary>
            /// <value>The timeout.</value>
            public TimeSpan? Timeout { get; }

            /// <summary>
            ///     Gets the delta Y.
            /// </summary>
            /// <value>The delta Y.</value>
            public double? DeltaY { get; }

            /// <summary>
            ///     Gets the delta X.
            /// </summary>
            /// <value>The delta X.</value>
            public double? DeltaX { get; }

            /// <summary>
            ///     Gets the maximum number of steps.
            /// </summary>
            /// <value>The maximum number of steps.</value>
            public int? MaximumNumberOfSteps { get; }

            /// <summary>
            ///     Should the search for a solution Stops?
            /// </summary>
            /// <param name="currentStep">The current step.</param>
            /// <param name="currentDeltaX">The current delta X.</param>
            /// <param name="currentY">The current Y.</param>
            /// <param name="utcStartTime">The UTC start time.</param>
            /// <param name="stopReasons">The stop reason.</param>
            /// <returns></returns>
            [SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
            public bool ShouldStop(int currentStep, double currentDeltaX, double currentY, DateTime utcStartTime,
                out StopReasons stopReasons)
            {
                if (currentY == 0.0)
                {
                    stopReasons = StopReasons.SolutionFound;
                    return true;
                }

                if (DeltaY.HasValue && Math.Abs(currentY) <= DeltaY.Value)
                {
                    stopReasons = StopReasons.SolutionFound | StopReasons.DeltaY;
                    return true;
                }

                if (DeltaX.HasValue && Math.Abs(currentDeltaX) <= DeltaX.Value)
                {
                    stopReasons = StopReasons.SolutionFound | StopReasons.DeltaX;
                    return true;
                }

                if (MaximumNumberOfSteps.HasValue && MaximumNumberOfSteps.Value < currentStep)
                {
                    stopReasons = StopReasons.MaximumNumberOfSteps;
                    return true;
                }

                if (Timeout.HasValue)
                {
                    var elapsed = DateTime.UtcNow - utcStartTime;
                    if (elapsed > Timeout.Value)
                    {
                        stopReasons = StopReasons.Timeout;
                        return true;
                    }
                }

                stopReasons = StopReasons.None;
                return false;
            }
        }
    }
}