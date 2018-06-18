using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using log4net;
using Newtonsoft.Json;

namespace Negri.Wot
{
    public class Clan : ClanPlataform
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Clan));

        private readonly List<Player> _players = new List<Player>();
        private DateTime _membershipMoment;

        private Dictionary<string, Player> _playersCache;
        private Dictionary<long, Player> _playersCacheById;
        private HistoricPoint[] _history = new HistoricPoint[0];

        public Clan(Plataform plataform, long clanId, string clanTag, string description = null)
            : base(plataform, clanId, clanTag)
        {
            Name = description ?? ClanTag;
        }

        /// <summary>
        /// Nome do clã, longo, pode conter unicode
        /// </summary>
        public string Name { get; set; }


        /// <summary>
        ///     Momento da atualização dessa informação, sempre UTC
        /// </summary>
        public DateTime Moment
        {
            get
            {
                if (!Players.Any())
                {
                    return FileMoment != DateTime.MinValue ? FileMoment : MembershipMoment;
                }

                var m = Players.Max(p => p.Moment);
                return m == DateTime.MinValue
                    ? (MembershipMoment == DateTime.MinValue ? FileMoment : MembershipMoment)
                    : m;
            }
        }

        public DateTime LastUpdate
        {
            get
            {
                var last = MembershipMoment;
                if (Moment > last)
                {
                    last = Moment;
                }
                return last;
            }
        }

        public DateTime Date => Moment.Date;

        public int TotalBattles
        {
            get { return Players.Sum(p => p.TotalBattles); }
        }
                
        public int MonthBattles
        {
            get { return Players.Sum(p => p.MonthBattles); }
        }

        public double TotalWinRate
        {
            get { return Players.Sum(p => p.TotalWinRate*p.TotalBattles)/TotalBattles; }
        }
                
        public double MonthWinRate
        {
            get
            {
                if (MonthBattles <= 0)
                {
                    return 0.0;
                }
                return Players.Where(p => p.ActiveOnMonth).Sum(p => p.MonthWinRate*p.MonthBattles)/MonthBattles;
            }
        }

        public double TotalWn8
        {
            get { return Players.Sum(p => p.TotalWn8*p.TotalBattles)/TotalBattles; }
        }
                
        public double MonthWn8
        {
            get { return Players.Where(p => p.ActiveOnMonth).Sum(p => p.MonthWn8*p.MonthBattles)/MonthBattles; }
        }

        /// <summary>
        /// The count of members, as returned by the WG API
        /// </summary>
        [JsonIgnore]
        public int AllMembersCount { get; set; }

        public int Count => _players.Count;
        
        public int Active => ActivePlayers.Count();

        public double ActivePercent => Active/(double) Count;

        public bool IsActive => (ActivePercent > 0.25) && (Active >= 4);

        /// <summary>
        ///     Idade dos dados
        /// </summary>
        public TimeSpan DataAge => DateTime.UtcNow - Moment;

        /// <summary>
        ///     Se os dados são obsoletos
        /// </summary>
        public bool IsObsolete => DataAge.TotalDays > 7.0;

        /// <summary>
        ///     Se os dados estão antigos (o dobro do delay)
        /// </summary>
        public bool IsOldData => DataAge.TotalDays > 2.0 * Delay;

        [JsonIgnore]
        public IEnumerable<Player> ActivePlayers
        {
            get { return Players.Where(p => p.IsActive); }
        }

        public int ActiveBattles
        {
            get { return ActivePlayers.Sum(p => p.MonthBattles); }
        }

        public double ActiveWinRate
        {
            get
            {
                if (ActiveBattles <= 0)
                {
                    return 0.0;
                }
                return ActivePlayers.Sum(p => p.MonthWinRate*p.MonthBattles)/ActiveBattles;
            }
        }

        public double ActiveWn8
        {
            get
            {
                if (ActiveBattles <= 0)
                {
                    return 0.0;
                }
                return ActivePlayers.Sum(p => p.MonthWn8*p.MonthBattles)/ActiveBattles;
            }
        }

        /// <summary>
        ///     Os 15 melhores jogadores
        /// </summary>
        [JsonIgnore]
        public IEnumerable<Player> Top15Players
        {
            get { return ActivePlayers.OrderByDescending(p => p.MonthWn8).Take(15); }
        }

        public int Top15Battles
        {
            get { return Top15Players.Sum(p => p.MonthBattles); }
        }

        public double Top15Wn8
        {
            get
            {
                if (Top15Battles <= 0)
                {
                    return 0.0;
                }
                return Top15Players.Sum(p => p.MonthWn8*p.MonthBattles)/Top15Battles;
            }
        }

        /// <summary>
        ///     Os 7 melhores jogadores
        /// </summary>
        [JsonIgnore]
        public IEnumerable<Player> Top7Players
        {
            get { return ActivePlayers.OrderByDescending(p => p.MonthWn8).Take(7); }
        }

        public int Top7Battles
        {
            get { return Top7Players.Sum(p => p.MonthBattles); }
        }

        public double Top7Wn8
        {
            get
            {
                if (Top7Battles <= 0)
                {
                    return 0.0;
                }
                return Top7Players.Sum(p => p.MonthWn8*p.MonthBattles)/Top7Battles;
            }
        }

        public string Country { get; set; }

        [JsonIgnore]
        public DateTime FileMoment { get; set; }

        /// <summary>
        ///     Valores do Clan do dia corrente
        /// </summary>
        [JsonIgnore]
        public HistoricPoint CurrentDay { get; set; }

        /// <summary>
        ///     Valores do Clan no dia anterior
        /// </summary>
        [JsonIgnore]
        public HistoricPoint PreviousDay { get; set; }

        /// <summary>
        ///     Valores do Clan na semana anterior
        /// </summary>
        [JsonIgnore]
        public HistoricPoint PreviousWeek { get; set; }

        /// <summary>
        ///     Valores do Clan no mês anterior
        /// </summary>
        [JsonIgnore]
        public HistoricPoint PreviousMonth { get; set; }

        [JsonIgnore]
        public int? DeltaDayActive => Active - PreviousDay?.Active;

        [JsonIgnore]
        public int? DeltaWeekActive => Active - PreviousWeek?.Active;

        [JsonIgnore]
        public int? DeltaMonthActive => Active - PreviousMonth?.Active;

        [JsonIgnore]
        public int? DeltaDayCount => Count - PreviousDay?.Count;

        [JsonIgnore]
        public int? DeltaWeekCount => Count - PreviousWeek?.Count;

        [JsonIgnore]
        public int? DeltaMonthCount => Count - PreviousMonth?.Count;

        [JsonIgnore]
        public double? DeltaDayActivePercent => ActivePercent - PreviousDay?.ActivityPercent;

        [JsonIgnore]
        public double? DeltaWeekActivePercent => ActivePercent - PreviousWeek?.ActivityPercent;

        [JsonIgnore]
        public double? DeltaMonthActivePercent => ActivePercent - PreviousMonth?.ActivityPercent;

        [JsonIgnore]
        public int? DeltaDayActiveBattles => ActiveBattles - PreviousDay?.ActiveBattles;

        [JsonIgnore]
        public int? DeltaWeekActiveBattles => ActiveBattles - PreviousWeek?.ActiveBattles;

        [JsonIgnore]
        public int? DeltaMonthActiveBattles => ActiveBattles - PreviousMonth?.ActiveBattles;

        [JsonIgnore]
        public int? DeltaDayTotalBattles => TotalBattles - PreviousDay?.TotalBattles;

        [JsonIgnore]
        public int? DeltaWeekTotalBattles => TotalBattles - PreviousWeek?.TotalBattles;

        [JsonIgnore]
        public int? DeltaMonthTotalBattles => TotalBattles - PreviousMonth?.TotalBattles;

        [JsonIgnore]
        public double? DeltaDayActiveWinRate => ActiveWinRate - PreviousDay?.ActiveWinRate;

        [JsonIgnore]
        public double? DeltaWeekActiveWinRate => ActiveWinRate - PreviousWeek?.ActiveWinRate;

        [JsonIgnore]
        public double? DeltaMonthActiveWinRate => ActiveWinRate - PreviousMonth?.ActiveWinRate;

        [JsonIgnore]
        public double? DeltaDayTotalWinRate => TotalWinRate - PreviousDay?.TotalWinRate;

        [JsonIgnore]
        public double? DeltaWeekTotalWinRate => TotalWinRate - PreviousWeek?.TotalWinRate;

        [JsonIgnore]
        public double? DeltaMonthTotalWinRate => TotalWinRate - PreviousMonth?.TotalWinRate;

        [JsonIgnore]
        public double? DeltaDayActiveWn8 => ActiveWn8 - PreviousDay?.ActiveWn8;

        [JsonIgnore]
        public double? DeltaWeekActiveWn8 => ActiveWn8 - PreviousWeek?.ActiveWn8;

        [JsonIgnore]
        public double? DeltaMonthActiveWn8 => ActiveWn8 - PreviousMonth?.ActiveWn8;

        [JsonIgnore]
        public double? DeltaDayTotalWn8 => TotalWn8 - PreviousDay?.TotalWn8;

        [JsonIgnore]
        public double? DeltaWeekTotalWn8 => TotalWn8 - PreviousWeek?.TotalWn8;

        [JsonIgnore]
        public double? DeltaMonthTotalWn8 => TotalWn8 - PreviousMonth?.TotalWn8;

        [JsonIgnore]
        public double? DeltaDayTop15Wn8 => Top15Wn8 - PreviousDay?.Top15Wn8;

        [JsonIgnore]
        public double? DeltaWeekTop15Wn8 => Top15Wn8 - PreviousWeek?.Top15Wn8;

        [JsonIgnore]
        public double? DeltaMonthTop15Wn8 => Top15Wn8 - PreviousMonth?.Top15Wn8;

        [JsonIgnore]
        public double? DeltaDayTop7Wn8 => Top7Wn8 - PreviousDay?.Top7Wn8;

        [JsonIgnore]
        public double? DeltaWeekTop7Wn8 => Top7Wn8 - PreviousWeek?.Top7Wn8;

        [JsonIgnore]
        public double? DeltaMonthTop7Wn8 => Top7Wn8 - PreviousMonth?.Top7Wn8;

        /// <summary>
        ///     Numero de Jogadores considerados como outliers
        /// </summary>
        public int NumberOfPatchedPlayers => Players.Count(p => p.IsPatched);

        /// <summary>
        ///     Se dados de jogadores foram "corrigidos"
        /// </summary>
        public bool IsPatched => NumberOfPatchedPlayers > 0;

        /// <summary>
        ///     Momento (UTC) em que os membros do clã foram pegos
        /// </summary>
        public DateTime MembershipMoment
        {
            get => _membershipMoment == DateTime.MinValue ? FileMoment : _membershipMoment;
            set => _membershipMoment = value;
        }

        [JsonIgnore]
        public Exception Error { get; set; }

        [JsonIgnore]
        public bool IsOnError => Error != null;

        /// <summary>
        ///     Quando foi criado o clã
        /// </summary>
        public DateTime? CreatedAtUtc { get; set; }

        /// <summary>
        ///     Antigo nome do clã no banco de dados
        /// </summary>
        public string OldTag { get; set; }

        /// <summary>
        /// Se o clã deve ficar invisivel
        /// </summary>
        public bool IsHidden { get; set; }

        public bool HasChangedTag
        {
            get
            {
                if (string.IsNullOrWhiteSpace(OldTag))
                {
                    return false;
                }
                return OldTag != ClanTag;
            }
        }
        

        /// <summary>
        ///     Verdadeiro se foi debandado
        /// </summary>
        [JsonIgnore]
        public bool IsDisbanded { get; set; }

        /// <summary>
        ///     Delay na captura de dados
        /// </summary>
        public double Delay { get; set; } = 1.0;

        #region Tier X Fields

        /// <summary>
        /// Tier X Total battles
        /// </summary>
        public int Tier10TotalBattles => Players.Sum(p => p.Tier10TotalBattles);

        /// <summary>
        /// Tier X Month Battles
        /// </summary>
        public int Tier10MonthBattles => Players.Sum(p => p.Tier10MonthBattles);

        /// <summary>
        /// Tier X Total Win Rate
        /// </summary>
        public double Tier10TotalWinRate => (Tier10TotalBattles > 0) ? Players.Sum(p => p.Tier10TotalWinRate * p.Tier10TotalBattles) * 1.0 / Tier10TotalBattles : 0.0;

        /// <summary>
        /// Tier X Month Win Rate
        /// </summary>
        public double Tier10MonthWinRate => (Tier10MonthBattles > 0) ? Players.Sum(p => p.Tier10MonthWinRate * p.Tier10MonthBattles) * 1.0 / Tier10MonthBattles : 0.0;

        /// <summary>
        /// Tier X Total WN8
        /// </summary>
        public double Tier10TotalWn8 => (Tier10TotalBattles > 0) ? Players.Sum(p => p.Tier10TotalWn8 * p.Tier10TotalBattles) * 1.0 / Tier10TotalBattles : 0.0;

        /// <summary>
        /// Tier X Month WN8
        /// </summary>
        public double Tier10MonthWn8 => (Tier10MonthBattles > 0) ? Players.Sum(p => p.Tier10MonthWn8 * p.Tier10MonthBattles) * 1.0 / Tier10MonthBattles : 0.0;

        #endregion

        #region Avg Tier

        /// <summary>
        /// The Recent Avg Tier of the top 15 players by WN8
        /// </summary>
        public double Top15AvgTier => Top15Battles > 0 ? Top15Players.Sum(p => p.MonthTier * p.MonthBattles) / Top15Battles : 0.0;

        [JsonIgnore]
        public double? DeltaDayTop15AvgTier => Top15AvgTier - PreviousDay?.Top15AvgTier;

        [JsonIgnore]
        public double? DeltaWeekTop15AvgTier => Top15AvgTier - PreviousWeek?.Top15AvgTier;

        [JsonIgnore]
        public double? DeltaMonthTop15AvgTier => Top15AvgTier - PreviousMonth?.Top15AvgTier;

        /// <summary>
        /// The Recent Avg Tier of the active players
        /// </summary>
        public double ActiveAvgTier => ActiveBattles > 0 ? ActivePlayers.Sum(p => p.MonthTier * p.MonthBattles) / ActiveBattles : 0.0;

        [JsonIgnore]
        public double? DeltaDayActiveAvgTier => ActiveAvgTier - PreviousDay?.ActiveAvgTier;

        [JsonIgnore]
        public double? DeltaWeekActiveAvgTier => ActiveAvgTier - PreviousWeek?.ActiveAvgTier;

        [JsonIgnore]
        public double? DeltaMonthActiveAvgTier => ActiveAvgTier - PreviousMonth?.ActiveAvgTier;

        /// <summary>
        /// The Overall Avg Tier of the entire clan
        /// </summary>
        public double TotalAvgTier => TotalBattles > 0 ? Players.Sum(p => p.TotalTier * p.TotalBattles) / TotalBattles : 0.0;


        [JsonIgnore]
        public double? DeltaDayTotalAvgTier => TotalAvgTier - PreviousDay?.TotalAvgTier;

        [JsonIgnore]
        public double? DeltaWeekTotalAvgTier => TotalAvgTier - PreviousWeek?.TotalAvgTier;

        [JsonIgnore]
        public double? DeltaMonthTotalAvgTier => TotalAvgTier - PreviousMonth?.TotalAvgTier;

        #endregion

        /// <summary>
        ///     Se a captura de dados está habilitada ou não
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Os jogadores do clã
        /// </summary>
        public Player[] Players
        {
            get => _players.ToArray();
            set
            {
                _players.Clear();
                if (value == null)
                {
                    return;
                }

                foreach (var player in value)
                {
                    Add(player);
                }
            }
        }

        public override string ToString()
        {
            return $"{ClanTag}@{Plataform}.{ClanId} - {Moment:yyyy-MM-dd HH:mm:ss}";
        }

        /// <summary>
        /// Diretorio raiz de salvamento
        /// </summary>
        /// <param name="resultDirectory"></param>
        /// <returns></returns>
        public string ToFile(string resultDirectory)
        {
            var fileName = Path.Combine(resultDirectory, "Clans",
                $"clan.{ClanTag}.json");

            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(fileName, json, Encoding.UTF8);
            return fileName;
        }

        public static Clan FromFile(string fileName, bool throwOnError = false)
        {
            if (fileName == null)
            {
                return null;
            }

            var clan = new Clan(Plataform.XBOX, 0, string.Empty);

            try
            {
                clan = JsonConvert.DeserializeObject<Clan>(File.ReadAllText(fileName, Encoding.UTF8));                
                return clan;
            }
            catch (Exception ex)
            {
                Log.Error(fileName, ex);
                var exx = new InvalidClanFileException(fileName, ex);
                if (throwOnError)
                {
                    throw exx;
                }
                clan.Error = exx;
                return clan;
            }
        }

        public void Add(Player player)
        {
            _playersCache = null;
            _playersCacheById = null;

            // Toma posse
            player.ClanTag = ClanTag;
            player.ClanId = ClanId;
            player.ClanDelay = Delay;
            _players.Add(player);
        }

        /// <summary>
        /// A historia do clã
        /// </summary>
        public HistoricPoint[] History
        {
            get => _history ?? new HistoricPoint[0];
            set
            {
                if (value == null)
                {
                    _history = null;
                    return;
                }
                AttachHistory(value);
            }
        }

        /// <summary>
        /// The average delay for getting data on active players
        /// </summary>
        public double ActivePlayersDelayHours => (Active == 0 ? 0 : ActivePlayers.Sum(p => p.Delay) / Active * Delay)*24.0;

        /// <summary>
        /// The average delay for getting data on all clan players
        /// </summary>
        public double AllPlayersDelayHours => (Count == 0 ? 0 : Players.Sum(p => p.Delay) / Count * Delay)*24.0;

        /// <summary>
        /// The reason the clan is disabled
        /// </summary>
        public DisabledReason DisabledReason { get; set; } = Wot.DisabledReason.NotDisabled;

        /// <summary>
        ///     Anexa o dia anterior, semana e mês.
        /// </summary>
        public void AttachHistory(IEnumerable<HistoricPoint>  history)
        {
            var a = history.Normalize().OrderByDescending(h => h.Date).ToArray();

            var date = Date;
            var day = date.AddDays(-1);
            var week = date.AddDays(-7);
            var month = date.AddDays(-28);

            CurrentDay = new HistoricPoint(this);

            if (a.Length > 0)
            {
                a[0] = CurrentDay;
                PreviousDay = a.FirstOrDefault(ch => ch.Date <= day);
                PreviousWeek = a.FirstOrDefault(ch => ch.Date <= week);
                PreviousMonth = a.FirstOrDefault(ch => ch.Date <= month);
            }
            
            _history = a;
        }

        public Player Get(long playerId)
        {
            if (_playersCacheById == null)
            {
                _playersCacheById = _players.ToDictionary(p => p.Id);
            }

            return _playersCacheById.TryGetValue(playerId, out var player) ? player : null;
        }

        public Player Get(string gamerTag)
        {
            if (_playersCache == null)
            {
                _playersCache = _players.ToDictionary(p => p.Name);
            }            

            if (_playersCache.TryGetValue(gamerTag, out var player))
            {
                return player;
            }
            if (_playersCache.TryGetValue(gamerTag.ToUpperInvariant(), out player))
            {
                return player;
            }
            if (_playersCache.TryGetValue(gamerTag.ToLowerInvariant(), out player))
            {
                return player;
            }

            return null;
        }
    }
}