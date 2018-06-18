using System;

namespace Negri.Wot.Diagnostics
{
    public abstract class AveragedCpuUsage
    {
        private double _instantaneousLoad;
        private double _sinceStartedLoad;

        /// <summary>
        /// Initializes a new instance of the <see cref="AveragedCpuUsage"/> class.
        /// </summary>
        protected AveragedCpuUsage()
        {
            ProbingTime = DateTime.UtcNow;
            ProcessorCount = Environment.ProcessorCount;
        }

        /// <summary>
        /// Gets or sets the probing time.
        /// </summary>
        /// <value>The probing time.</value>
        public DateTime ProbingTime { get; set; }

        /// <summary>
        /// The averaged, since start, CPU load. In the range [0; 1]
        /// </summary>
        /// <value>The load.</value>
        public double SinceStartedLoad
        {
            get { return _sinceStartedLoad; }
            set
            {
                // pequenas diferenças são toleradas, em maquinas multi-core é possivel que os valores fiquem ligeiramente fora do previsto.
                if (value < -0.01)
                {
                    _sinceStartedLoad = double.NaN;
                    return;
                }
                if (value > 1.01)
                {
                    _sinceStartedLoad = double.NaN;
                    return;
                }

                if (value < 0.0) value = 0.0;
                if (value > 1.0) value = 1.0;
                _sinceStartedLoad = value;
            }
        }

        /// <summary>
        /// Gets or sets the instantaneous load, that is, averaged on 0.5s
        /// </summary>
        /// <value>The instantaneous load.</value>
        public double InstantaneousLoad
        {
            get { return _instantaneousLoad; }
            set
            {
                // pequenas diferenças são toleradas, em maquinas multi-core é possivel que os valores fiquem ligeiramente fora do previsto.
                if (value < -0.01)
                {
                    _instantaneousLoad = double.NaN;
                    return;
                }
                if (value > 1.01)
                {
                    _instantaneousLoad = double.NaN;
                    return;
                }

                if (value < 0.0) value = 0.0;
                if (value > 1.0) value = 1.0;
                _instantaneousLoad = value;
            }
        }

        /// <summary>
        /// Gets or sets the user time.
        /// </summary>
        /// <value>The user time.</value>
        public TimeSpan UserTime { get; set; }

        /// <summary>
        /// Gets or sets the user time total seconds.
        /// </summary>
        /// <value>The user time total seconds.</value>
        public double UserTimeTotalSeconds
        {
            get { return UserTime.TotalSeconds; }
            set { UserTime = TimeSpan.FromSeconds(value); }
        }

        /// <summary>
        /// Gets or sets the idle time.
        /// </summary>
        /// <value>The idle time.</value>
        public TimeSpan IdleTime { get; set; }

        /// <summary>
        /// Gets or sets the idle time total seconds.
        /// </summary>
        /// <value>The idle time total seconds.</value>
        public double IdleTimeTotalSeconds
        {
            get { return IdleTime.TotalSeconds; }
            set { IdleTime = TimeSpan.FromSeconds(value); }
        }

        /// <summary>
        /// Gets the total time.
        /// </summary>
        /// <value>The total time.</value>
        public TimeSpan TotalTime
        {
            get { return UserTime + PrivilegedTime + IdleTime; }
        }

        /// <summary>
        /// Gets or sets the privileged time.
        /// </summary>
        /// <value>The privileged time.</value>
        public TimeSpan PrivilegedTime { get; set; }

        /// <summary>
        /// Gets or sets the privileged time total seconds.
        /// </summary>
        /// <value>The privileged time total seconds.</value>
        public double PrivilegedTimeTotalSeconds
        {
            get { return PrivilegedTime.TotalSeconds; }
            set { PrivilegedTime = TimeSpan.FromSeconds(value); }
        }

        /// <summary>
        /// The UTC time the machine started
        /// </summary>
        /// <value>The start time.</value>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets or sets the name of the machine.
        /// </summary>
        /// <value>The name of the machine.</value>
        public int ProcessorCount { get; set; }
    }
}