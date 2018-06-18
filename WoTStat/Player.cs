using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Negri.Wot.Tanks;
using Newtonsoft.Json;

namespace Negri.Wot
{
    public class Player : IEquatable<Player>, ICloneable
    {        
        
        public Player()
        {
            Rank = Rank.Private;
        }

        /// <summary>
        /// Constroi
        /// </summary>
        /// <param name="plataform">Plataforma</param>
        /// <param name="playerId">Id no jogo</param>
        /// <param name="name">Gamer Tag</param>
        public Player(Plataform plataform, long playerId, string name)
        {
            Id = playerId;
            Name = name;
            Plataform = plataform;
            Moment = DateTime.UtcNow;
        }

        public Plataform Plataform { get; set; } = Plataform.XBOX;

        /// <summary>
        ///     Referencia (que é comum ser nula) ao clã do jogador
        /// </summary>
        public string ClanTag { get; set; }

        /// <summary>
        ///     Referencia (que é comum ser nula) ao clã do jogador
        /// </summary>
        public long? ClanId { get; set; }

        public Rank Rank { get; set; }
        
        public long Id { get; set; }

        public string Name { get; set; }

        public int TotalBattles { get; set; }
        
        public int MonthBattles { get; set; }

        public int WeekBattles { get; set; }

        public double TotalWinRate { get; set; }
        
        public double MonthWinRate { get; set; }

        public double WeekWinRate { get; set; }

        /// <summary>
        /// Time in battle last month
        /// </summary>
        public TimeSpan MonthTime { get; set; }

        /// <summary>
        /// Time in batles last week
        /// </summary>
        public TimeSpan WeekTime { get; set; }

        /// <summary>
        /// Total time in battle
        /// </summary>
        public TimeSpan TotalTime { get; set; }

        /// <summary>
        /// Overall WN8
        /// </summary>                
        public double TotalWn8 { get; set; }
        
        /// <summary>
        /// Last Month (28 days) WN8
        /// </summary>
        public double MonthWn8 { get; set; }

        /// <summary>
        /// Last Week WN8
        /// </summary>
        public double WeekWn8 { get; set; }

        /// <summary>
        /// Overall Average Tier
        /// </summary>
        public double TotalTier { get; set; }

        /// <summary>
        /// Last Month (28 days) Average Tier
        /// </summary>
        public double MonthTier { get; set; }

        /// <summary>
        /// Last Week (7 days) Average Tier
        /// </summary>
        public double WeekTier { get; set; }

        [JsonIgnore]
        public bool ActiveOnMonth => MonthBattles > 0;

        /// <summary>
        ///     Ativo no mês se fizer pelo menos uma batalha por dia util
        /// </summary>
        public bool IsActive => MonthBattles >= 21;

        /// <summary>
        ///     Momento UTC em que o dado foi obtido
        /// </summary>
        public DateTime Moment { get; set; }
        
        /// <summary>
        ///     Se os dados foram emendados
        /// </summary>
        public bool IsPatched { get; set; }

        /// <summary>
        /// Origem dos dados de WN8 do jogador
        /// </summary>
        public PlayerDataOrigin Origin { get; set; } = PlayerDataOrigin.WotInfo;

        /// <summary>
        /// Tempo, em horas ajustadas ao Delay total, dos dados
        /// </summary>
        [JsonIgnore]
        public double AdjustedAgeHours { get; set; }

        public DateTime Date => Moment.Date;

        /// <summary>
        /// Delay na busca do jogador (1 = 24h)
        /// </summary>
        public double Delay { get; set; } = 1;

        /// <summary>
        /// Delay geral do clã (1 = 24h)
        /// </summary>
        public double ClanDelay { get; set; } = 1;

        /// <summary>
        /// Delay total aplicado a esse jogador (1 = 24h)
        /// </summary>
        public double TotalDelay => Delay * ClanDelay;

        /// <summary>
        /// The estimated time for the next update moment
        /// </summary>
        public DateTime NextMoment => Moment.AddHours(24.0 * TotalDelay);

        #region Tier X Fields

        /// <summary>
        /// Tier X Total battles
        /// </summary>
        public int Tier10TotalBattles { get; set; }

        /// <summary>
        /// Tier X Month Battles
        /// </summary>
        public int Tier10MonthBattles { get; set; }

        /// <summary>
        /// Tier 10 Time
        /// </summary>
        public TimeSpan Tier10TotalTime { get; set; }

        /// <summary>
        /// Tier 10 Month Time
        /// </summary>
        public TimeSpan Tier10MonthTime { get; set; }

        /// <summary>
        /// Tier X Total Win Rate
        /// </summary>
        public double Tier10TotalWinRate { get; set; }

        /// <summary>
        /// Tier X Month Win Rate
        /// </summary>
        public double Tier10MonthWinRate { get; set; }

        /// <summary>
        /// Tier X Total WN8
        /// </summary>
        public double Tier10TotalWn8 { get; set; }

        /// <summary>
        /// Tier X Month WN8
        /// </summary>
        public double Tier10MonthWn8 { get; set; }

        /// <summary>
        /// Performance Details on each vehicle for the player
        /// </summary>
        public TankPlayerPeriods Performance { get; set; }

        /// <summary>
        /// The player overall Url
        /// </summary>
        [JsonIgnore]
        public string PlayerOverallUrl
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ClanTag))
                {
                    return $"https://console.worldoftanks.com/en/stats/players/{Id}/";
                }
                return $"{ClanUrl}/Commanders/{Id}/All";
            }
        }

        /// <summary>
        /// The clan url
        /// </summary>
        [JsonIgnore]
        public string ClanUrl
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ClanTag))
                {
                    return string.Empty;
                }
                return $"https://{(Plataform == Plataform.PS ? "ps." : "")}wotclans.com.br/Clan/{ClanTag}";
            }
        }

        /// <summary>
        /// The age of the data
        /// </summary>
        [JsonIgnore]
        public TimeSpan Age => DateTime.UtcNow - Moment;

        #endregion

        public bool Equals(Player other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Plataform == other.Plataform && Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((Player) obj);
        }

        [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode")]
        public override int GetHashCode()
        {
            unchecked
            {
                return ((int) Plataform*397) ^ Id.GetHashCode();
            }
        }

        /// <inheritdoc />
        public object Clone()
        {
            return new Player(Plataform, Id, Name)
            {
                Origin = Origin,
                Delay = Delay,
                AdjustedAgeHours = AdjustedAgeHours,
                ClanDelay = ClanDelay,
                ClanId = ClanId,
                ClanTag = ClanTag,
                IsPatched = IsPatched,
                Moment = Moment,
                Rank = Rank,

                TotalTime = TotalTime,
                TotalBattles = TotalBattles,
                TotalWinRate = TotalWinRate,
                TotalWn8 = TotalWn8,
                TotalTier = TotalTier,

                MonthTime = MonthTime,
                MonthBattles = MonthBattles,
                MonthWinRate = MonthWinRate,
                MonthWn8 = MonthWn8,
                MonthTier = MonthTier,

                WeekTime = WeekTime,
                WeekBattles = WeekBattles,
                WeekWinRate = WeekWinRate,
                WeekWn8 = WeekWn8,
                WeekTier = WeekTier,

                Tier10TotalTime = Tier10TotalTime,
                Tier10TotalBattles =  Tier10TotalBattles,
                Tier10MonthWinRate = Tier10MonthWinRate,
                Tier10TotalWn8 = Tier10TotalWn8,

                Tier10MonthTime = Tier10MonthTime,
                Tier10MonthBattles = Tier10MonthBattles,                                
                Tier10TotalWinRate = Tier10TotalWinRate,                                
                Tier10MonthWn8 = Tier10MonthWn8
            };
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var ci = CultureInfo.InvariantCulture;

            return
                $"{Id}\t{Name}\t{TotalBattles}\t{MonthBattles}\t0\t{TotalWinRate.ToString("r", ci)}\t{MonthWinRate.ToString("r", ci)}" +
                $"\t0\t{TotalWn8.ToString("r", ci)}\t{MonthWn8.ToString("r", ci)}\t0" +
                $"\t{Moment:o}\t{(IsPatched ? 1 : 0)}\t{(IsPatched ? 1 : 0)}\t{(IsPatched ? "0" : string.Empty)}\t{(int) Rank}\t{(int) Origin}";
        }



        public static Player Parse(string line)
        {
            var f = line.Split('\t');

            var player = new Player
            {
                Id = long.Parse(f[2]),
                Name = f[3],
                TotalBattles = int.Parse(f[4]),
                MonthBattles = int.Parse(f[5]),
                // f[6] Não mais usado
                TotalWinRate = double.Parse(f[7], CultureInfo.InvariantCulture),
                MonthWinRate = double.Parse(f[8], CultureInfo.InvariantCulture),
                // f[9] Não mais usado
                TotalWn8 = double.Parse(f[10], CultureInfo.InvariantCulture),
                MonthWn8 = double.Parse(f[11], CultureInfo.InvariantCulture),
                // f[12] Não mais usado
            };

            if (f.Length > 13)
            {
                player.Moment = DateTime.Parse(f[13], CultureInfo.InvariantCulture).ToUniversalTime();
            }
            if (f.Length > 14)
            {
                // Ignore. Não mais usado. Era IsSuspicious                
            }
            if (f.Length > 15)
            {
                player.IsPatched = int.Parse(f[15]) == 1;
            }
            if ((f.Length > 16) && (player.IsPatched))
            {
                // Ignore. Não mais usado. Era OriginalMonthWn8                
            }
            if (f.Length > 17)
            {
                player.Rank = (Rank) int.Parse(f[17]);
            }

            if (f.Length > 18)
            {
                player.Origin = (PlayerDataOrigin) int.Parse(f[18]);
            }

            return player;
        }

        public bool CanSave()
        {
            return (TotalBattles != 0) || (MonthBattles != 0) || 
                   (!(Math.Abs(TotalWinRate) < 1e-10));
        }

        public bool Check(Player previousPlayer, bool patch)
        {
            if (previousPlayer.MonthBattles < 21)
            {
                return false;
            }
            if (MonthBattles < 21)
            {
                return false;
            }
            if (previousPlayer.IsPatched)
            {
                return false;
            }
            if (previousPlayer.MonthWn8 <= 0)
            {
                return false;
            }
            if (MonthWn8 <= 0)
            {
                return false;
            }

            if ((Date - previousPlayer.Date).TotalDays > 7)
            {
                // Dados anteriores com mais de uma semana
                return false;
            }

            var delta = MonthWn8 - previousPlayer.MonthWn8;
            var deltaPercent = delta/previousPlayer.MonthWn8;

            if ((-0.25 <= deltaPercent) && (deltaPercent <= +0.40))
            {
                return false;
            }

            if (!patch)
            {
                return false;
            }

            // Patch dos dados mensais
            MonthBattles = previousPlayer.MonthBattles;
            MonthWn8 = previousPlayer.MonthWn8;
            MonthWinRate = previousPlayer.MonthWinRate;

            if (TotalBattles < previousPlayer.TotalBattles)
            {
                // Totais estragados tambem...
                TotalBattles = previousPlayer.TotalBattles;
                TotalWn8 = previousPlayer.TotalWn8;
                TotalWinRate = previousPlayer.TotalWinRate;
            }

            IsPatched = true;
            return true;
        }

        /// <summary>
        /// Calculate all properties
        /// </summary>
        /// <param name="wn8ExpectedValues">The reference tanks</param>
        public void Calculate(Wn8ExpectedValues wn8ExpectedValues)
        {
            PruneTanks(wn8ExpectedValues);
            Performance.ExpectedValues = wn8ExpectedValues;
            Performance.CalculateAllTanks();

            TotalTime = Performance.GetTime();
            TotalBattles = Performance.GetBattles();
            TotalWinRate = Performance.GetWinRate();
            TotalTier = Performance.GetTier();
            TotalWn8 = Performance.GetWn8();

            MonthTime = Performance.GetTime(ReferencePeriod.Month);
            MonthBattles = Performance.GetBattles(ReferencePeriod.Month);
            MonthWinRate = Performance.GetWinRate(ReferencePeriod.Month);
            MonthTier = Performance.GetTier(ReferencePeriod.Week);
            MonthWn8 = Performance.GetWn8(ReferencePeriod.Month);

            WeekTime = Performance.GetTime(ReferencePeriod.Week);
            WeekBattles = Performance.GetBattles(ReferencePeriod.Week);
            WeekWinRate = Performance.GetWinRate(ReferencePeriod.Week);
            WeekTier = Performance.GetTier(ReferencePeriod.Week);
            WeekWn8 = Performance.GetWn8(ReferencePeriod.Week);

            Tier10TotalBattles = Performance.GetBattles(ReferencePeriod.All, 10);
            Tier10TotalTime = Performance.GetTime(ReferencePeriod.All, 10);
            Tier10TotalWinRate = Performance.GetWinRate(ReferencePeriod.All, 10);
            Tier10TotalWn8 = Performance.GetWn8(ReferencePeriod.All, 10);

            Tier10MonthBattles = Performance.GetBattles(ReferencePeriod.Month, 10);
            Tier10MonthTime = Performance.GetTime(ReferencePeriod.Month, 10);
            Tier10MonthWinRate = Performance.GetWinRate(ReferencePeriod.Month, 10);
            Tier10MonthWn8 = Performance.GetWn8(ReferencePeriod.Month, 10);
            
        }
              
        private void PruneTanks(Wn8ExpectedValues wn8ExpectedValues)
        {
            void CompleteOnDictionary(IDictionary<long, TankPlayerStatistics> dic)
            {
                foreach (var kv in dic)
                {
                    var t = wn8ExpectedValues[kv.Key];
                    if (t == null)
                    {
                        continue;
                    }

                    kv.Value.Plataform = t.Plataform;
                    kv.Value.TankId = t.TankId;
                    kv.Value.Name = t.Name;
                    kv.Value.Tag = t.Tag;
                    kv.Value.Tier = t.Tier;
                    kv.Value.Type = t.Type;
                    kv.Value.Nation = t.Nation;
                    kv.Value.IsPremium = t.IsPremium;
                }
            }

            CompleteOnDictionary(Performance.All);
            CompleteOnDictionary(Performance.Month);
            CompleteOnDictionary(Performance.Week);

            void CleanIncompleteTanks(IDictionary<long, TankPlayerStatistics> dic)
            {
                var toRemove = new HashSet<long>();
                foreach (var kv in dic)
                {
                    if (kv.Value.TankId == 0)
                    {
                        toRemove.Add(kv.Key);
                    }
                }

                foreach (var tankId in toRemove)
                {
                    dic.Remove(tankId);
                }
            }

            CleanIncompleteTanks(Performance.All);
            CleanIncompleteTanks(Performance.Month);
            CleanIncompleteTanks(Performance.Week);
        }

        public bool HasTank(long tankId)
        {
            return Performance?.All != null && Performance.All.ContainsKey(tankId);
        }
    }
}