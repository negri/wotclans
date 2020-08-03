using System;

namespace Negri.Wot.Diagnostics
{
    public class DataDiagnostic
    {
        public DataDiagnostic()
        {
            Moment=DateTime.UtcNow;
        }

        public DateTime Moment { get; set; }

        public int PlayersQueueLength { get; set; }

        public int MembershipQueueLength { get; set; }

        public int CalculateQueueLength { get; set; }

        public int TotalPlayers { get; set; }

        public double ScheduledPlayersPerDay { get; set; }

        public double ScheduledPlayersPerHour { get; set; }

        public double AvgPlayersPerHourLastDay { get; set; }

        public double AvgPlayersPerHourLast6Hours { get; set; }

        public double AvgPlayersPerHourLast2Hours { get; set; }

        public double AvgPlayersPerHourLastHour { get; set; }

        public int TotalEnabledClans { get; set; }

        /// <summary>
        /// The effective number of hours to retrieve data for players and clans with a delay of 1 day, on the last 48h
        /// </summary>
        public double Last48HDelay { get; set; }

        /// <summary>
        /// The effective number of hours to retrieve data for players and clans with a delay of 1 day, on the last 72h
        /// </summary>
        public double Last72HDelay { get; set; }

        /// <summary>
        /// The effective number of hours to retrieve data for players and clans with a delay of 1 day, on the last 96h
        /// </summary>
        public double Last96HDelay { get; set; }
    }
}