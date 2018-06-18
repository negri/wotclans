using System;
using System.Diagnostics;
using System.Threading;

namespace Negri.Wot.Diagnostics
{
    public static class CpuUsage
    {
        
        /// <summary>
        ///     Gets the process total time.
        /// </summary>
        public static AveragedProcessCpuUsage GetProcessTotalTime()
        {
            //Está sendo feito assíncronamente pois assim joga para uma outra thread e 
            // "ignora" o impersonate.
            AveragedProcessCpuUsageDelegate async = GetProcessTotalTimeAsync;
            var asyncResult = async.BeginInvoke(null, null);

            var result = async.EndInvoke(asyncResult);

            return result;
        }


        /// <summary>
        ///     Retorna informações do consumo de memória pelo processo
        /// </summary>
        /// <returns></returns>
        public static ProcessMemoryUsage GetProcessMemoryInformation()
        {
            var proc = Process.GetCurrentProcess();

            var pmu = new ProcessMemoryUsage
            {
                WorkingSet = proc.WorkingSet64,
                Private = proc.PrivateMemorySize64,
                Virtual = proc.VirtualMemorySize64,
                Paged = proc.PagedMemorySize64,
                NonpagedSystem = proc.NonpagedSystemMemorySize64,
                PagedSystem = proc.PagedSystemMemorySize64,
                ThreadCount = proc.Threads.Count,
                HandleCount = proc.HandleCount
            };

            try
            {
                pmu.ModuleCount = proc.Modules.Count;
            }
            catch (Exception)
            {
                pmu.ModuleCount = 0;
            }

            return pmu;
        }

        private static AveragedProcessCpuUsage GetProcessTotalTimeAsync()
        {
            var process = Process.GetCurrentProcess();
            var historicStartTime = process.StartTime.ToUniversalTime();

            var startOfInstantMeasure = DateTime.UtcNow;
            TimeSpan startPrivileged, startUser;
            GetProcessTimes(out startPrivileged, out startUser);

            Thread.Sleep(TimeSpan.FromMilliseconds(500));

            TimeSpan endPrivileged, endUser;
            GetProcessTimes(out endPrivileged, out endUser);
            var now = DateTime.UtcNow;

            var endOfInstantMeasure = now;

            var totalLife = now - historicStartTime;
            var instantLife = endOfInstantMeasure - startOfInstantMeasure;

            var cpu = new AveragedProcessCpuUsage
            {
                StartTime = historicStartTime,
                PrivilegedTime = endPrivileged,
                UserTime = endUser,
                IdleTime = TimeSpan.Zero,
                SinceStartedLoad = GetLoad(Environment.ProcessorCount, totalLife, TimeSpan.Zero, TimeSpan.Zero, endPrivileged, endUser),
                InstantaneousLoad = GetLoad(Environment.ProcessorCount, instantLife, startPrivileged, startUser, endPrivileged, endUser)
            };

            return cpu;
        }

        private static double GetLoad(int processorCount, TimeSpan life, TimeSpan startPrivileged, TimeSpan startUser, TimeSpan endPrivileged, TimeSpan endUser)
        {
            var avaiableTotal = life.TotalMilliseconds * processorCount;
            var usedPrivileged = endPrivileged.TotalMilliseconds - startPrivileged.TotalMilliseconds;
            var usedUser = endUser.TotalMilliseconds - startUser.TotalMilliseconds;
            return (usedPrivileged + usedUser) / avaiableTotal;
        }

        private static void GetProcessTimes(out TimeSpan privileged, out TimeSpan user)
        {
            var process = Process.GetCurrentProcess();
            privileged = process.PrivilegedProcessorTime;
            user = process.UserProcessorTime;
        }


        private delegate AveragedProcessCpuUsage AveragedProcessCpuUsageDelegate();
    }
}