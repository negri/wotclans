using System;
using System.Collections.Generic;
using System.Linq;
using Negri.Wot.WgApi;
using Newtonsoft.Json;

namespace Negri.Wot.Tanks
{
    /// <summary>
    /// Os valores jogados por um jogador no período total, mês (28 dias) e 7 dias
    /// </summary>
    public class TankPlayerPeriods
    {
        /// <summary>
        /// Overall
        /// </summary>
        public Dictionary<long, TankPlayerStatistics> All { get; set; } = new Dictionary<long, TankPlayerStatistics>();

        /// <summary>
        /// Last month (28 days)
        /// </summary>
        public Dictionary<long, TankPlayerStatistics> Month { get; set; } = new Dictionary<long, TankPlayerStatistics>();

        /// <summary>
        /// Last Week (7 days)
        /// </summary>
        public Dictionary<long, TankPlayerStatistics> Week { get; set; } = new Dictionary<long, TankPlayerStatistics>();

        /// <summary>
        /// The expected values to calculate WN8
        /// </summary>
        [JsonIgnore]
        public Wn8ExpectedValues ExpectedValues { get; set; }

        /// <summary>
        /// Calcula o WN8 de cada tanque
        /// </summary>
        public void CalculateAllTanks()
        {
            foreach (var tank in All.Values)
            {
                tank.Wn8 = ExpectedValues.CalculateWn8(tank);
            }
            foreach (var tank in Month.Values)
            {
                tank.Wn8 = ExpectedValues.CalculateWn8(tank);
            }
            foreach (var tank in Week.Values)
            {
                tank.Wn8 = ExpectedValues.CalculateWn8(tank);
            }
        }

        public int GetBattles(ReferencePeriod period = ReferencePeriod.All, int minTier = 1, int maxTier = 10, bool? isPremium = null, TankType? type = null)
        {
            var tanks = GetTanks(period, minTier, maxTier, isPremium, type);
            return (int)tanks.Sum(t => t.Battles);
        }

        public TimeSpan GetTime(ReferencePeriod period = ReferencePeriod.All, int minTier = 1, int maxTier = 10, bool? isPremium = null, TankType? type = null)
        {
            var tanks = GetTanks(period, minTier, maxTier, isPremium, type);
            return TimeSpan.FromSeconds(tanks.Sum(t => t.BattleLifeTimeSeconds));
        }

        public double GetWinRate(ReferencePeriod period = ReferencePeriod.All, int minTier = 1, int maxTier = 10, bool? isPremium = null, TankType? type = null)
        {
            var tanks = GetTanks(period, minTier, maxTier, isPremium, type).ToArray();
            if (!tanks.Any())
            {
                return 0.0;
            }
            return tanks.Sum(t => t.Wins * 1.0) / tanks.Sum(t => t.Battles);
        }

        public double GetWn8(ReferencePeriod period = ReferencePeriod.All, int minTier = 1, int maxTier = 10, bool? isPremium = null, TankType? type = null)
        {
            var tanks = GetTanks(period, minTier, maxTier, isPremium, type);
            return ExpectedValues.CalculateWn8(tanks);
        }

        public double GetTier(ReferencePeriod period = ReferencePeriod.All, int minTier = 1, int maxTier = 10, bool? isPremium = null, TankType? type = null)
        {
            if (minTier == maxTier)
            {
                return minTier;
            }

            var tanks = GetTanks(period, minTier, maxTier, isPremium, type).ToArray();
            if (!tanks.Any())
            {
                return 0.0;
            }
            return tanks.Sum(t => t.Tier * 1.0 * t.Battles) / tanks.Sum(t => t.Battles);
        }

        public IEnumerable<TankPlayerStatistics> GetTanks(ReferencePeriod period = ReferencePeriod.All, int minTier = 1, int maxTier = 10, bool? isPremium = null, TankType? type = null, int? minBattles = null, Nation? nation = null)
        {
            Dictionary<long, TankPlayerStatistics> dic;
            switch (period)
            {
                case ReferencePeriod.Month:
                    dic = Month;
                    break;
                case ReferencePeriod.Week:
                    dic = Week;
                    break;
                case ReferencePeriod.All:
                    dic = All;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(period), period, null);
            }

            var tanks = dic.Values.Where(t => (minTier <= t.Tier) && (t.Tier <= maxTier));
            if (isPremium.HasValue)
            {
                tanks = tanks.Where(t => t.IsPremium == isPremium.Value);
            }

            if (type.HasValue)
            {
                tanks = tanks.Where(t => t.Type == type.Value);
            }

            if (minBattles.HasValue)
            {
                tanks = tanks.Where(t => t.Battles >= minBattles.Value);
            }

            if (nation.HasValue)
            {
                tanks = tanks.Where(t => t.Nation == nation.Value);
            }

            return tanks.ToArray();
        }

        private static TankPlayerStatistics[] FilterTopTanks(TankPlayerStatistics[] all, int minNumberOfTanks, PremiumSelection includePremiums, Func<TankPlayerStatistics, double> orderBy)
        {
            if (all.Length <= 0)
            {
                return Array.Empty<TankPlayerStatistics>();
            }

            if (all.Length == 1)
            {
                return all;
            }

            if (all.Length < minNumberOfTanks)
            {
                // arredonda para o múltiplo de 5 inferior
                minNumberOfTanks = ((all.Length) / 5) * 5;

                if (minNumberOfTanks <= 0)
                {
                    // fazer o quê?
                    minNumberOfTanks = all.Length;
                }
            }

            var battlesRanges = new[] { 2500, 1000, 500, 250, 100, 50, 25, 10, 5, 1 };

            foreach (var minBattles in battlesRanges)
            {
                var top = all.Where(t => t.Battles >= minBattles && includePremiums.Filter(t.IsPremium))
                    .OrderByDescending(orderBy).Take(minNumberOfTanks).ToArray();
                if (top.Length >= minNumberOfTanks)
                {
                    return top;
                }
            }

            return Array.Empty<TankPlayerStatistics>();
        }

        /// <summary>
        /// Top tanks by Kill/Death
        /// </summary>
        public IEnumerable<TankPlayerStatistics> GetTopTanksByKillDeathRatio(ReferencePeriod period = ReferencePeriod.All, int minNumberOfTanks = 5, int minTier = 1, int maxTier = 10, PremiumSelection includePremiums = PremiumSelection.OnlyRegular)
        {

            var all = GetTanks(period, minTier, maxTier).ToArray();

            var top = FilterTopTanks(all, minNumberOfTanks, includePremiums, t => t.KillDeathRatio);

            if (top.Any())
            {
                return top;
            }

            // new player, very few higher tiers
            top = GetTanks(period).Where(t => t.Battles >= 1).OrderByDescending(t => t.KillDeathRatio).Take(minNumberOfTanks).ToArray();
            return top;
        }

        /// <summary>
        /// Top tanks by WN8
        /// </summary>
        public IEnumerable<TankPlayerStatistics> GetTopTanks(ReferencePeriod period = ReferencePeriod.All, int minNumberOfTanks = 5, int minTier = 1, int maxTier = 10, PremiumSelection includePremiums = PremiumSelection.OnlyRegular)
        {

            var all = GetTanks(period, minTier, maxTier).ToArray();

            var top = FilterTopTanks(all, minNumberOfTanks, includePremiums, t => t.Wn8);

            if (top.Any())
            {
                return top;
            }

            // new player, very few higher tiers
            top = GetTanks(period).Where(t => t.Battles >= 1).OrderByDescending(t => t.Wn8).Take(minNumberOfTanks).ToArray();
            return top;
        }

        public TankPlayerStatistics GetBestTank(ReferencePeriod period = ReferencePeriod.All)
        {
            return GetTopTanks(period, 25, 5).FirstOrDefault() ?? All.Values.OrderByDescending(t => t.Wn8).FirstOrDefault();
        }

        public void PurgeMedals()
        {
            PurgeMedals(All);
            PurgeMedals(Month);
            PurgeMedals(Week);
        }

        private void PurgeMedals(Dictionary<long, TankPlayerStatistics> tanks)
        {
            foreach (var t in tanks.Values)
            {
                t.Achievements = null;
                t.Ribbons = null;
            }
        }

        /// <summary>
        /// <c>True</c> if Achievements are present on tanks
        /// </summary>
        public bool HasMedals => IsMedalsPopulated(All) || IsMedalsPopulated(Month) || IsMedalsPopulated(Week);

        private static bool IsMedalsPopulated(Dictionary<long, TankPlayerStatistics> tanks)
        {
            if (tanks == null)
            {
                return false;
            }

            if (tanks.Values.Any(t => (t.Achievements != null) && (t.Achievements.Count > 0)))
            {
                return true;
            }
            if (tanks.Values.Any(t => (t.Ribbons != null) && (t.Ribbons.Count > 0)))
            {
                return true;
            }

            return false;
        }

        public IEnumerable<TankPlayerStatistics> WithMedal(ReferencePeriod period, string medalCode)
        {
            var all = All.Values;
            if (period == ReferencePeriod.Month)
            {
                all = Month.Values;
            }
            else if (period == ReferencePeriod.Week)
            {
                all = Week.Values;
            }

            return all.Where(t => t.WithMedal(medalCode));
        }
    }
}