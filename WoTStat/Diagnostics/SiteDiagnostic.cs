using System;
using System.Collections.Generic;
using System.Linq;

namespace Negri.Wot.Diagnostics
{
    public class SiteDiagnostic
    {
        public SiteDiagnostic() { }

        public SiteDiagnostic(IEnumerable<Clan> clans)
        {
            ServerMoment = DateTime.UtcNow;
            ProcessMemoryUsage = CpuUsage.GetProcessMemoryInformation();
            AveragedProcessCpuUsage = CpuUsage.GetProcessTotalTime();

            if (clans != null)
            {
                var a = clans as Clan[] ?? clans.ToArray();
                if (a.Length > 0)
                {
                    ClansCount = a.Length;
                    PlayersCount = a.Where(c => !c.IsObsolete).Sum(c => c.Count);
                    MostRecentClanMoment = a.Where(c => !c.IsObsolete).Max(c => c.Moment);
                    ClansWithPlayersUpdatedOnLastHour = a.Count(c => (DateTime.UtcNow - c.Moment).TotalMinutes <= 60);
                    ClansWithAnyUpdatedOnLastHour = a.Count(c => (DateTime.UtcNow - c.LastUpdate).TotalMinutes <= 60);
                }
            }
        }

        public DateTime ServerMoment { get; set; }

        public int DataAgeMinutes => (int)(DateTime.UtcNow - MostRecentClanMoment).TotalMinutes;

        public DateTime MostRecentClanMoment { get; set; }

        public int ClansCount { get; set; }

        public int PlayersCount { get; set; }

        /// <summary>
        /// Clãs que tiveram dados de jogadores atualizados
        /// </summary>
        public int ClansWithPlayersUpdatedOnLastHour { get; set; }

        /// <summary>
        /// Clãs que tiveram qualquer atualização na ultima hora (membros ou 
        /// </summary>
        public int ClansWithAnyUpdatedOnLastHour { get; set; }

        public AveragedProcessCpuUsage AveragedProcessCpuUsage { get; set; }

        public ProcessMemoryUsage ProcessMemoryUsage { get; set; }
        
        public DateTime TanksMoELastDate { get; set; }

        public DateTime TankLeadersLastDate { get; set; }
    }
}