using System.Diagnostics;

namespace Negri.Wot.Diagnostics
{
    public class AveragedProcessCpuUsage : AveragedCpuUsage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AveragedProcessCpuUsage"/> class.
        /// </summary>
        public AveragedProcessCpuUsage()
        {
            ProcessId = Process.GetCurrentProcess().Id;
        }

        /// <summary>
        /// Gets or sets the process id.
        /// </summary>
        /// <value>The process id.</value>
        public int ProcessId { get; set; }

        /// <summary>
        /// Formata o conteúdo numa única linha
        /// </summary>
        public override string ToString()
        {
            return
                string.Format(
                    "Process {6} started {0:o}: Load={1:P1}, Total:{2:N0}s, User:{3:N0}s, Privileged:{4:N0}s, Idle: {5:N0}s",
                    StartTime, SinceStartedLoad, TotalTime.TotalSeconds, UserTime.TotalSeconds,
                    PrivilegedTime.TotalSeconds, IdleTime.TotalSeconds, ProcessId);
        }
    }
}