using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Negri.Wot.WgApi;
using Newtonsoft.Json;

namespace Negri.Wot.Tanks
{
    /// <summary>
    /// Os valores jogados por um jogador no periodo total, mês (28 dias) e 7 dias
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
            return (int) tanks.Sum(t => t.Battles);
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

        private IEnumerable<TankPlayerStatistics> GetTanks(ReferencePeriod period = ReferencePeriod.All, int minTier = 1, int maxTier = 10, bool? isPremium = null, TankType? type = null)
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
                tanks = tanks.Where(t => t.IsPremium = isPremium.Value);
            }

            if (type.HasValue)
            {
                tanks = tanks.Where(t => t.Type == type.Value);
            }

            return tanks;
        }

        public IEnumerable<TankPlayerStatistics> GetTopTanks(ReferencePeriod period = ReferencePeriod.All, int minNumberOfTanks = 5, int minTier = 1, int maxTier = 10, bool includePremiums = false)
        {
            
            var all = GetTanks(period, minTier, maxTier).ToArray();

            var top = all.Where(t => t.Battles >= 2500 && (!t.IsPremium || includePremiums)).OrderByDescending(t => t.Wn8).Take(minNumberOfTanks).ToArray();
            if (top.Length >= minNumberOfTanks)
            {
                return top;
            }

            top = all.Where(t => t.Battles >= 1000 && (!t.IsPremium || includePremiums)).OrderByDescending(t => t.Wn8).Take(minNumberOfTanks).ToArray();
            if (top.Length >= minNumberOfTanks)
            {
                return top;
            }

            top = all.Where(t => t.Battles >= 500 && (!t.IsPremium || includePremiums)).OrderByDescending(t => t.Wn8).Take(minNumberOfTanks).ToArray();
            if (top.Length >= minNumberOfTanks)
            {
                return top;
            }

            top = all.Where(t => t.Battles >= 250 && (!t.IsPremium || includePremiums)).OrderByDescending(t => t.Wn8).Take(minNumberOfTanks).ToArray();
            if (top.Length >= minNumberOfTanks)
            {
                return top;
            }

            top = all.Where(t => t.Battles >= 100 && (!t.IsPremium || includePremiums)).OrderByDescending(t => t.Wn8).Take(minNumberOfTanks).ToArray();
            if (top.Length >= minNumberOfTanks)
            {
                return top;
            }

            top = all.Where(t => t.Battles >= 50 && (!t.IsPremium || includePremiums)).OrderByDescending(t => t.Wn8).Take(minNumberOfTanks).ToArray();
            if (top.Length >= minNumberOfTanks)
            {
                return top;
            }

            top = all.Where(t => t.Battles >= 25 && (!t.IsPremium || includePremiums)).OrderByDescending(t => t.Wn8).Take(minNumberOfTanks).ToArray();
            if (top.Length >= minNumberOfTanks)
            {
                return top;
            }

            top = all.Where(t => t.Battles >= 10 && (!t.IsPremium || includePremiums)).OrderByDescending(t => t.Wn8).Take(minNumberOfTanks).ToArray();
            if (top.Length >= minNumberOfTanks)
            {
                return top;
            }

            top = all.Where(t => t.Battles >= 5 && (!t.IsPremium || includePremiums)).OrderByDescending(t => t.Wn8).Take(minNumberOfTanks).ToArray();
            if (top.Length >= minNumberOfTanks)
            {
                return top;
            }

            // new player, very few higher tiers
            top = GetTanks(period, 1, 10).Where(t => t.Battles >= 1).OrderByDescending(t => t.Wn8).Take(minNumberOfTanks).ToArray();                        
            return top;
        }

        public TankPlayerStatistics GetBestTank(ReferencePeriod period = ReferencePeriod.All)
        {
            return GetTopTanks(period, 25, 5, 10, false).FirstOrDefault() ?? All.Values.OrderByDescending(t => t.Wn8).FirstOrDefault();            
        }

    }
}