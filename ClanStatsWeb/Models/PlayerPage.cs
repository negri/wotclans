using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Negri.Wot.Tanks;

namespace Negri.Wot.Models
{
    public class PlayerPage
    {
        /// <summary>
        ///     The full player
        /// </summary>
        public Player Player { get; set; }

        /// <summary>
        ///     The player from clan data
        /// </summary>
        public Player ClanPlayer => Clan.Get(Player.Id);

        /// <summary>
        ///     The clan
        /// </summary>
        public Clan Clan { get; set; }
        
        /// <summary>
        ///     To provide tanks reference values
        /// </summary>
        public Wn8ExpectedValues Wn8ExpectedValues { get; set; }

        /// <summary>
        ///     To provide MoE reference values
        /// </summary>
        public IDictionary<long, TankMoe> MoEs { get; set; }


        public Tank BestOverallTank
        {
            get
            {
                var tank = (Tank)Player.Performance.GetBestTank() ?? Wn8ExpectedValues.AllTanks.GetRandom();
                Debug.Assert(tank != null);
                return tank;
            }
        }

        public string BestOverallTankImageUrl
        {
            get
            {
                var t = BestOverallTank;
                return $"https://wxpcdn.gcdn.co/dcont/tankopedia/{t.Nation.ToStringUrl()}/{t.Tag}.png";
            }
        }

        public Tank BestMonthTank
        {
            get
            {
                var tank = (Tank)Player.Performance.GetBestTank(ReferencePeriod.Month) ?? Wn8ExpectedValues.AllTanks.GetRandom();
                Debug.Assert(tank != null);
                return tank;
            }
        }

        public string BestMonthTankImageUrl
        {
            get
            {
                var t = BestMonthTank;
                return $"https://wxpcdn.gcdn.co/dcont/tankopedia/{t.Nation.ToStringUrl()}/{t.Tag}.png";
            }
        }

        public string WoTStatConsoleOverallUrl { get; set; }
        public string WotStatConsoleRecentUrl { get; set; }
        public string WoTInfoOverallUrl { get; set; }
        public string WoTInfoRecentUrl { get; set; }
        public string WoTInfoHistoryUrl { get; set; }
        public string WotStatConsoleHistoryUrl { get; set; }
        public string ExternalUrl { get; set; }

        public DateTime NextLeaderboardCutMoment
        {
            get
            {
                var nextTuesday = DateTime.UtcNow.Date.AddDays(2);
                while (nextTuesday.DayOfWeek != DayOfWeek.Tuesday)
                {
                    nextTuesday = nextTuesday.AddDays(1);
                }

                return nextTuesday.AddDays(-1);
            }
        }

        public Leader[] OnLeaderboard { get; set; } = new Leader[0];
        public TankPlayerStatistics[] LeaderIfPlayABattle { get; set; } = new TankPlayerStatistics[0];
        public TankPlayerStatistics[] LeaderIfFewMoreGames { get; set; } = new TankPlayerStatistics[0];
        public TankPlayerStatistics[] LeaderIfFewMoreDamage { get; set; } = new TankPlayerStatistics[0];
        public TankPlayerStatistics[] LeaderOnNextUpdate { get; set; } = new TankPlayerStatistics[0];

        public bool CanBeOnLeaderboard => OnLeaderboard.Any() || LeaderIfPlayABattle.Any() || LeaderIfFewMoreGames.Any() || LeaderIfFewMoreDamage.Any() ||
                                          LeaderOnNextUpdate.Any();

        public DateTime NextLeaderboardPublishMoment
        {
            get
            {
                var nextTuesday = DateTime.UtcNow.Date.AddDays(2);
                while (nextTuesday.DayOfWeek != DayOfWeek.Tuesday)
                {
                    nextTuesday = nextTuesday.AddDays(1);
                }

                return nextTuesday.AddHours(2);
            }
        }
    }
}