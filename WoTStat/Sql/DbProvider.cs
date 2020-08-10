using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Linq;
using log4net;
using Negri.Wot.Achievements;
using Negri.Wot.Diagnostics;
using Negri.Wot.Tanks;
using Negri.Wot.WgApi;
using Tank = Negri.Wot.Tanks.Tank;

namespace Negri.Wot.Sql
{
    /// <summary>
    ///     Obtem clãs no Sql
    /// </summary>
    public class DbProvider : DataAccessBase
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(DbProvider));

        public DbProvider(string connectionString) : base(connectionString)
        {
        }

        public IEnumerable<ClanBaseInformation> GetClanMembershipUpdateOrder(int maxClans, int ageHours)
        {
            return Get(transaction => GetClanMembershipUpdateOrder(maxClans, ageHours, transaction).ToArray());
        }

        private static IEnumerable<ClanBaseInformation> GetClanMembershipUpdateOrder(int maxClans, int ageHours,
            SqlTransaction t)
        {
            using (var cmd = new SqlCommand("Main.GetClanMembershipUpdateOrder", t.Connection, t))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 5 * 60;
                cmd.Parameters.AddWithValue("@max", maxClans);
                cmd.Parameters.AddWithValue("@ageHours", ageHours);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        yield return new ClanBaseInformation(reader.GetNonNullValue<long>(3),
                            reader.GetNonNullValue<string>(0));
                    }
                }
            }
        }

        /// <summary>
        /// Get the player id from his gamer tag
        /// </summary>
        public long? GetPlayerIdByName(Platform platform, string gamerTag)
        {
            return Get(t => GetPlayerIdByName(platform, gamerTag, t));
        }

        private static long? GetPlayerIdByName(Platform platform, string gamerTag, SqlTransaction t)
        {
            const string query =
                "select top (1) PlayerId from Main.Player where (PlataformId = @platformId) and (GamerTag = @gamerTag);";
            using (var cmd = new SqlCommand(query, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 15;
                cmd.Parameters.AddWithValue("@platformId", (int) platform);
                cmd.Parameters.AddWithValue("@gamerTag", gamerTag);
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return reader.GetNonNullValue<long>(0);
                    }
                }
            }

            return null;
        }

        public IEnumerable<TankReference> GetTanksReferences(DateTime? date = null, long? tankId = null, bool includeMoe = true, bool includeHistogram = false,
            bool includeLeaders = true, int topLeaders = 50)
        {
            return Get(transaction =>
                GetTanksReferences(date, tankId, includeMoe, includeHistogram, includeLeaders, topLeaders, transaction)
                    .ToArray());
        }

        /// <summary>
        /// Enumerate all tanks
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Tank> EnumTanks()
        {
            return Get(transaction => EnumTanks(transaction).ToArray());
        }

        private static IEnumerable<Tank> EnumTanks(SqlTransaction t)
        {
            var tanks = new List<Tank>(600);

            const string sql =
                "select TankId, [Name], ShortName, NationId, Tier, TypeId, Tag, IsPremium from Tanks.Tank where (IsJoke = 0);";
            using (var cmd = new SqlCommand(sql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 5 * 60;
                
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var tank = new Tank
                        {
                            Platform = Platform.Console,
                            TankId = reader.GetNonNullValue<long>(0),
                            FullName = reader.GetNonNullValue<string>(1),
                            Name = reader.GetNonNullValue<string>(2),
                            Nation = reader.GetNonNullValue<Nation>(3),
                            Tier = reader.GetNonNullValue<int>(4),
                            Type = reader.GetNonNullValue<TankType>(5),
                            Tag = reader.GetNonNullValue<string>(6),
                            IsPremium = reader.GetNonNullValue<bool>(7)
                        };
                        tanks.Add(tank);
                    }
                }
            }

            return tanks;
        }

        /// <summary>
        /// Retorna os jogadores de um clã que tem partidas com um tanque especifico.
        /// </summary>
        public IEnumerable<Player> GetClanPlayerIdsOnTank(long clanId, long tankId)
        {
            return Get(t => GetClanPlayerIdsOnTank(clanId, tankId, t));
        }

        private static IEnumerable<Player> GetClanPlayerIdsOnTank(long clanId, long tankId,
            SqlTransaction t)
        {
            var list = new List<Player>();

            const string sql = "select distinct p.PlayerId, rcc.GamerTag, pp.PlataformId " +
                               "from Performance.PlayerDate p " +
                               "inner join Main.RecentClanCompositionStats rcc on (rcc.PlayerId = p.PlayerId) " +
                               "inner join Main.Player pp on pp.PlayerId = p.PlayerId " +
                               "where rcc.ClanId = @clanId and rcc.IsActive = 1 and p.TankId = @tankId " +
                               "order by rcc.GamerTag;";

            using (var cmd = new SqlCommand(sql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.AddWithValue("@tankId", tankId);
                cmd.Parameters.AddWithValue("@clanId", clanId);

                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(
                            new Player(
                                r.GetNonNullValue<Platform>(2),
                                r.GetNonNullValue<long>(0),
                                r.GetNonNullValue<string>(1)));
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Retrieve the leaderboard for a given tank
        /// </summary>
        public IEnumerable<Leader> GetLeaderboard(long tankId, int top = 25, string flagCode = null,
            int skip = 0)
        {
            return Get(t => GetLeaderboard(tankId, top, flagCode, skip, t));
        }

        private static IEnumerable<Leader> GetLeaderboard(long tankId, int top, string flagCode,
            int skip, SqlTransaction t)
        {
            Log.Debug($"{nameof(GetLeaderboard)}({tankId}, {top}, {flagCode}, {skip})...");

            var list = new List<Leader>(top);

            using (var cmd = new SqlCommand("Performance.GetTopPlayersAll", t.Connection, t))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@tankId", tankId);
                cmd.Parameters.AddWithValue("@top", top);
                if (!string.IsNullOrWhiteSpace(flagCode))
                {
                    cmd.Parameters.AddWithValue("@flagCode", flagCode);
                }

                using (var reader = cmd.ExecuteReader())
                {
                    var order = 1;
                    while (reader.Read())
                    {
                        list.Add(
                            new Leader
                            {
                                Order = order++,
                                TankId = tankId,
                                PlayerId = reader.GetNonNullValue<long>(0),
                                GamerTag = reader.GetNonNullValue<string>(1),
                                ClanId = reader.GetNonNullValue<long>(2),
                                ClanTag = reader.GetNonNullValue<string>(3),
                                ClanFlag = reader.GetValueOrDefault<string>(4),
                                LastBattle = reader.GetNonNullValue<DateTime>(5).ChangeKind(DateTimeKind.Utc),
                                BattleTime = TimeSpan.FromSeconds(reader.GetNonNullValue<long>(6)),
                                Battles = reader.GetNonNullValue<long>(7),
                                TotalDamage = (double) reader.GetNonNullValue<decimal>(8),
                                DamageAssisted = (double) reader.GetNonNullValue<decimal>(9),
                                Kills = (double) reader.GetNonNullValue<decimal>(10),
                                MaxKills = reader.GetNonNullValue<long>(11),
                                Spotted = (double) reader.GetNonNullValue<decimal>(12),
                                Date = reader.GetNonNullValue<DateTime>(13).ChangeKind(DateTimeKind.Utc),
                                Name = reader.GetNonNullValue<string>(14),
                                FullName = reader.GetNonNullValue<string>(15),
                                IsPremium = reader.GetNonNullValue<bool>(16),
                                Tag = reader.GetNonNullValue<string>(17),
                                Tier = reader.GetNonNullValue<int>(18),
                                Type = reader.GetNonNullValue<TankType>(19),
                                Nation = reader.GetNonNullValue<Nation>(20),
                                Platform = reader.GetNonNullValue<Platform>(21)
                            });
                    }
                }
            }

            return list;
        }

        public IEnumerable<TankPlayerStatistics> GetPlayerHistoryByTank(Platform platform, long playerId, long tankId)
        {
            return Get(t => GetPlayerHistoryByTank(platform, playerId, tankId, t));
        }

        private static IEnumerable<TankPlayerStatistics> GetPlayerHistoryByTank(Platform platform, long playerId,
            long tankId, SqlTransaction t)
        {
            var l = new List<TankPlayerStatistics>();

            const string sql = "select TankId, Battles, Damage, Win, Frag, Spot, Def, " +
                               "DamageAssistedTrack, DamageAssistedRadio, Shots, Hits, Piercings, ExplosionHits, CapturePoints, Losses, " +
                               "DamageReceived, SurvivedBattles, NoDamageDirectHitsReceived, DirectHitsReceived, ExplosionHitsReceived, PiercingsReceived, " +
                               "LastBattle, TreesCut, MaxFrags, MarkOfMastery, BattleLifeTimeSeconds " +
                               "from Performance.GetTanksStatsForWn8Hist(@playerId, @tankId) " +
                               "order by LastBattle desc;";
            using (var cmd = new SqlCommand(sql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.AddWithValue("@playerId", playerId);
                cmd.Parameters.AddWithValue("@tankId", tankId);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        l.Add(new TankPlayerStatistics
                        {
                            TankId = reader.GetNonNullValue<long>(0),
                            Battles = reader.GetNonNullValue<long>(1),
                            DamageDealt = reader.GetNonNullValue<long>(2),
                            Wins = reader.GetNonNullValue<long>(3),
                            Kills = reader.GetNonNullValue<long>(4),
                            Spotted = reader.GetNonNullValue<long>(5),
                            DroppedCapturePoints = reader.GetNonNullValue<long>(6),
                            DamageAssistedTrack = reader.GetNonNullValue<long>(7),
                            DamageAssistedRadio = reader.GetNonNullValue<long>(8),
                            Shots = reader.GetNonNullValue<long>(9),
                            Hits = reader.GetNonNullValue<long>(10),
                            Piercings = reader.GetNonNullValue<long>(11),
                            ExplosionHits = reader.GetNonNullValue<long>(12),
                            CapturePoints = reader.GetNonNullValue<long>(13),
                            Losses = reader.GetNonNullValue<long>(14),
                            DamageReceived = reader.GetNonNullValue<long>(15),
                            SurvivedBattles = reader.GetNonNullValue<long>(16),
                            NoDamageDirectHitsReceived = reader.GetNonNullValue<long>(17),
                            DirectHitsReceived = reader.GetNonNullValue<long>(18),
                            ExplosionHitsReceived = reader.GetNonNullValue<long>(19),
                            PiercingsReceived = reader.GetNonNullValue<long>(20),
                            LastBattle = reader.GetNonNullValue<DateTime>(21).ChangeKind(DateTimeKind.Utc),
                            TreesCut = reader.GetNonNullValue<long>(22),
                            MaxFrags = reader.GetNonNullValue<long>(23),
                            MarkOfMastery = reader.GetNonNullValue<long>(24),
                            BattleLifeTimeSeconds = reader.GetNonNullValue<long>(25),
                        });
                    }
                }
            }

            return l;
        }

        /// <summary>
        /// Returns the player history, as tracked by the site
        /// </summary>
        public IEnumerable<Player> GetPlayerHistory(long playerId)
        {
            return Get(t => GetPlayerHistory(playerId, t));
        }

        private static IEnumerable<Player> GetPlayerHistory(long playerId, SqlTransaction t)
        {
            const string sql =
                "select ClanId, ClanTag, Moment, TotalBattles, MonthBattles, TotalWn8, MonthWn8 from Main.GetPlayerHistoryById(@playerId) order by moment desc;";

            var l = new List<Player>();

            using (var cmd = new SqlCommand(sql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.AddWithValue("@playerId", playerId);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        l.Add(new Player()
                        {
                            Id = playerId,
                            ClanId = reader.GetValueOrDefault<long?>(0),
                            ClanTag = reader.GetValueOrDefault<string>(1),
                            Moment = reader.GetNonNullValue<DateTime>(2).RemoveKind(),
                            TotalBattles = reader.GetNonNullValue<int>(3),
                            MonthBattles = reader.GetNonNullValue<int>(4),
                            TotalWn8 = reader.GetNonNullValue<double>(5),
                            MonthWn8 = reader.GetNonNullValue<double>(6)
                        });
                    }
                }
            }

            return l;
        }

        private static IEnumerable<TankReference> GetTanksReferences(DateTime? date, long? tankId, bool includeMoe, bool includeHistogram, bool includeLeaders,
            int topLeaders,
            SqlTransaction t)
        {
            var references = new Dictionary<long, TankReference>();

            // Get the overall data
            using (var cmd = new SqlCommand("Performance.GetTanksReferences", t.Connection, t))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 5 * 60;
                cmd.Parameters.AddWithValue("@period", ReferencePeriod.All.ToString());
                
                if (date.HasValue)
                {
                    cmd.Parameters.AddWithValue("@date", date.Value);
                }

                if (tankId.HasValue)
                {
                    cmd.Parameters.AddWithValue("@tankId", tankId.Value);
                }

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var tr = new TankReference
                        {
                            Date = reader.GetNonNullValue<DateTime>(0),
                            TankId = reader.GetNonNullValue<long>(2),
                            Platform = Platform.Console,
                            Name = reader.GetNonNullValue<string>(3),
                            Tier = reader.GetNonNullValue<int>(4),
                            Type = reader.GetNonNullValue<TankType>(5),
                            Nation = reader.GetNonNullValue<Nation>(7),
                            IsPremium = reader.GetNonNullValue<bool>(9),
                            Tag = reader.GetNonNullValue<string>(10),
                            Spotted = reader.GetNonNullValue<double>(11),
                            DamageAssistedTrack = reader.GetNonNullValue<double>(12),
                            DamageAssistedRadio = reader.GetNonNullValue<double>(13),
                            CapturePoints = reader.GetNonNullValue<double>(14),
                            DroppedCapturePoints = reader.GetNonNullValue<double>(15),
                            TotalBattles = reader.GetNonNullValue<long>(16),
                            TotalPlayers = reader.GetNonNullValue<int>(17),
                            TotalWins = reader.GetNonNullValue<long>(18),
                            TotalLosses = reader.GetNonNullValue<long>(19),
                            Kills = reader.GetNonNullValue<double>(20),
                            TotalSurvivedBattles = reader.GetNonNullValue<long>(21),
                            DamageDealt = reader.GetNonNullValue<double>(22),
                            DamageReceived = reader.GetNonNullValue<double>(23),
                            TotalTime = TimeSpan.FromHours(reader.GetNonNullValue<double>(24)),
                            TreesCut = reader.GetNonNullValue<double>(25),
                            MaxKills = reader.GetNonNullValue<double>(26),
                            MarkOfMastery = reader.GetNonNullValue<double>(27),
                            Hits = reader.GetNonNullValue<double>(28),
                            NoDamageDirectHitsReceived = reader.GetNonNullValue<double>(29),
                            ExplosionHits = reader.GetNonNullValue<double>(30),
                            Piercings = reader.GetNonNullValue<double>(31),
                            Shots = reader.GetNonNullValue<double>(32),
                            ExplosionHitsReceived = reader.GetNonNullValue<double>(33),
                            XP = reader.GetNonNullValue<double>(34),
                            DirectHitsReceived = reader.GetNonNullValue<double>(35),
                            PiercingReceived = reader.GetNonNullValue<double>(36),
                            FullName = reader.GetNonNullValue<string>(37)
                        };
                        references.Add(tr.TankId, tr);
                    }
                }
            }

            // Obtém os MoEs na data
            var marksOfExcellence = includeMoe
                ? GetMoe(date, tankId, t).ToDictionary(m => m.TankId)
                : new Dictionary<long, TankMoe>();

            // WN8, Histograms and Leaders
            foreach (var tr in references.Values)
            {
                if (marksOfExcellence.TryGetValue(tr.TankId, out var moe))
                {
                    tr.MoeHighMark = moe.HighMarkDamage;
                }

                // WN8
                const string sqlWn8 =
                    "select Def, Frag, Spot, Damage, WinRate, Origin from Tanks.CurrentWn8Expected where TankId = @tankId;";
                using (var cmd = new SqlCommand(sqlWn8, t.Connection, t))
                {
                    cmd.Parameters.AddWithValue("@tankId", tr.TankId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            tr.Wn8Values = new Wn8TankExpectedValues
                            {
                                Platform = tr.Platform,
                                TankId = tr.TankId,
                                Name = tr.Name,
                                FullName = tr.FullName,
                                Tag = tr.Tag,
                                Nation = tr.Nation,
                                Type = tr.Type,
                                Tier = tr.Tier,
                                IsPremium = tr.IsPremium,
                                Def = reader.GetNonNullValue<double>(0),
                                Frag = reader.GetNonNullValue<double>(1),
                                Spot = reader.GetNonNullValue<double>(2),
                                Damage = reader.GetNonNullValue<double>(3),
                                WinRate = reader.GetNonNullValue<double>(4),
                                Origin = reader.GetNonNullValue<Wn8TankExpectedValuesOrigin>(5)
                            };

                            tr.AverageWn8 = tr.Wn8Values.GetWn8(tr.DamageDealt, tr.WinRatio, tr.Kills, tr.Spotted,
                                tr.DroppedCapturePoints);
                            tr.TargetDamageAverage = tr.Wn8Values.GetTargetDamage(Wn8Rating.Average);
                            tr.TargetDamageGood = tr.Wn8Values.GetTargetDamage(Wn8Rating.Good);
                            tr.TargetDamageGreat = tr.Wn8Values.GetTargetDamage(Wn8Rating.Great);
                            tr.TargetDamageUnicum = tr.Wn8Values.GetTargetDamage(Wn8Rating.Unicum);
                            tr.TargetDamageSuperUnicum = tr.Wn8Values.GetTargetDamage(Wn8Rating.SuperUnicum);
                        }
                    }
                }

                // Histogram
                if ((tr.TotalPlayers > 100) && (tr.TotalBattles > 1000) && (tr.Tier >= 5) && includeHistogram)
                {
                    // Obtém histogramas se o tanque for significativo
                    Log.DebugFormat("Obtendo histogramas do tank {0}.{1}...", tr.TankId, tr.Name);

                    var metrics = new[]
                    {
                        "Kills", "DamageDealt", "Spotted", "DroppedCapturePoints", "DamageAssistedTrack",
                        "DamageAssistedRadio", "DamageAssisted", "TotalDamage", "WinRatio"
                    };

                    foreach (var metric in metrics)
                    {
                        using (var cmd = new SqlCommand("Performance.GetHistogramAll", t.Connection, t))
                        {
                            cmd.CommandType = CommandType.StoredProcedure;
                            cmd.Parameters.AddWithValue("@tankId", tr.TankId);
                            cmd.Parameters.Add(new SqlParameter("@metric", SqlDbType.VarChar, 50) {Value = metric});
                            cmd.Parameters.AddWithValue("@levels", 50);

                            var hist = new Histogram();
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    hist.Add(
                                        (double) reader.GetNonNullValue<decimal>(0),
                                        (double) reader.GetNonNullValue<decimal>(1),
                                        reader.GetNonNullValue<int>(2));
                                }
                            }

                            if (hist.Count > 0)
                            {
                                switch (metric)
                                {
                                    case "Kills":
                                        tr.KillsHistogram = hist;
                                        break;
                                    case "DamageDealt":
                                        tr.DamageDealtHistogram = hist;
                                        break;
                                    case "Spotted":
                                        tr.SpottedHistogram = hist;
                                        break;
                                    case "DroppedCapturePoints":
                                        tr.DroppedCapturePointsHistogram = hist;
                                        break;
                                    case "DamageAssistedTrack":
                                        tr.DamageAssistedTrackHistogram = hist;
                                        break;
                                    case "DamageAssistedRadio":
                                        tr.DamageAssistedRadioHistogram = hist;
                                        break;
                                    case "DamageAssisted":
                                        tr.DamageAssistedHistogram = hist;
                                        break;
                                    case "TotalDamage":
                                        tr.TotalDamageHistogram = hist;
                                        break;
                                    case "WinRatio":
                                        tr.WinRatioHistogram = hist;
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException();
                                }
                            }
                        }
                    }
                }

                // Obtém lideres
                if ((tr.TotalPlayers > 100) && (tr.TotalBattles > 1000) && (tr.Tier >= 5) && includeLeaders)
                {
                    tr.Leaders = GetLeaderboard(tr.TankId, topLeaders, null, 0, t).ToArray();
                }
            }

            // Get Last Month data
            using (var cmd = new SqlCommand("Performance.GetTanksReferences", t.Connection, t))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 5 * 60;
                cmd.Parameters.AddWithValue("@period", ReferencePeriod.Month.ToString());
                
                if (date.HasValue)
                {
                    cmd.Parameters.AddWithValue("@date", date.Value);
                }

                if (tankId.HasValue)
                {
                    cmd.Parameters.AddWithValue("@tankId", tankId.Value);
                }

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var tr = new TankReferenceBasic
                        {
                            TankId = reader.GetNonNullValue<long>(2),
                            Platform = Platform.Console,
                            Name = reader.GetNonNullValue<string>(3),
                            Tier = reader.GetNonNullValue<int>(4),
                            Type = reader.GetNonNullValue<TankType>(5),
                            Nation = reader.GetNonNullValue<Nation>(7),
                            IsPremium = reader.GetNonNullValue<bool>(9),
                            Tag = reader.GetNonNullValue<string>(10),
                            Spotted = reader.GetNonNullValue<double>(11),
                            DamageAssistedTrack = reader.GetNonNullValue<double>(12),
                            DamageAssistedRadio = reader.GetNonNullValue<double>(13),
                            CapturePoints = reader.GetNonNullValue<double>(14),
                            DroppedCapturePoints = reader.GetNonNullValue<double>(15),
                            TotalBattles = reader.GetNonNullValue<long>(16),
                            TotalPlayers = reader.GetNonNullValue<int>(17),
                            TotalWins = reader.GetNonNullValue<long>(18),
                            TotalLosses = reader.GetNonNullValue<long>(19),
                            Kills = reader.GetNonNullValue<double>(20),
                            TotalSurvivedBattles = reader.GetNonNullValue<long>(21),
                            DamageDealt = reader.GetNonNullValue<double>(22),
                            DamageReceived = reader.GetNonNullValue<double>(23),
                            TotalTime = TimeSpan.FromHours(reader.GetNonNullValue<double>(24)),
                            TreesCut = reader.GetNonNullValue<double>(25),
                            MaxKills = reader.GetNonNullValue<double>(26),
                            MarkOfMastery = reader.GetNonNullValue<double>(27),
                            Hits = reader.GetNonNullValue<double>(28),
                            NoDamageDirectHitsReceived = reader.GetNonNullValue<double>(29),
                            ExplosionHits = reader.GetNonNullValue<double>(30),
                            Piercings = reader.GetNonNullValue<double>(31),
                            Shots = reader.GetNonNullValue<double>(32),
                            ExplosionHitsReceived = reader.GetNonNullValue<double>(33),
                            XP = reader.GetNonNullValue<double>(34),
                            DirectHitsReceived = reader.GetNonNullValue<double>(35),
                            PiercingReceived = reader.GetNonNullValue<double>(36),
                            FullName = reader.GetNonNullValue<string>(37)
                        };

                        var all = references[tr.TankId];
                        tr.AverageWn8 = all.Wn8Values.GetWn8(tr.DamageDealt, tr.WinRatio, tr.Kills, tr.Spotted,
                            tr.DroppedCapturePoints);

                        all.LastMonth = tr;
                    }
                }
            }

            return references.Values;
        }

        public long? GetPlayerIdByDiscordId(long userId)
        {
            return Get(t => GetPlayerIdByDiscordId(userId, t));
        }

        private static long? GetPlayerIdByDiscordId(long userId, SqlTransaction t)
        {
            const string sql = "select PlayerId from Discord.UserPlayer where UserId = @userId;";
            using (var cmd = new SqlCommand(sql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.AddWithValue("@userId", userId);
                var o = cmd.ExecuteScalar();
                if (o == null)
                {
                    return null;
                }

                return (long) o;
            }
        }

        public IEnumerable<TankMoe> GetMoe(DateTime? date = null, long? tankId = null)
        {
            return Get(transaction => GetMoe(date, tankId, transaction).ToArray());
        }

        private static IEnumerable<TankMoe> GetMoe(DateTime? date, long? tankId, SqlTransaction t)
        {
            using (var cmd = new SqlCommand("Performance.GetMoE", t.Connection, t))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 5 * 60;
                cmd.Parameters.AddWithValue("@methodId", 4); // Percentile Method
                
                if (date.HasValue)
                {
                    cmd.Parameters.AddWithValue("@date", date.Value);
                }

                if (tankId.HasValue)
                {
                    cmd.Parameters.AddWithValue("@tankId", tankId.Value);
                }

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        yield return new TankMoe
                        {
                            Date = reader.GetNonNullValue<DateTime>(0),
                            Name = reader.GetNonNullValue<string>(1),
                            Tier = reader.GetNonNullValue<int>(2),
                            Type = reader.GetNonNullValue<TankType>(3),
                            Nation = reader.GetNonNullValue<Nation>(5),
                            IsPremium = reader.GetNonNullValue<bool>(7),
                            Tag = reader.GetNonNullValue<string>(8),
                            HighMarkDamage = reader.GetNonNullValue<double>(9),
                            NumberOfDates = reader.GetNonNullValue<int>(10),
                            NumberOfBattles = reader.GetNonNullValue<long>(11),
                            TankId = reader.GetNonNullValue<long>(12),
                            FullName = reader.GetNonNullValue<string>(13)
                        };
                    }
                }
            }
        }

        public Clan GetClan(string clanTag)
        {
            var clanId = GetClanId(clanTag);
            if (!clanId.HasValue)
            {
                return null;
            }

            return GetClan(clanId.Value);
        }

        private long? GetClanId(string clanTag)
        {
            return Get(transaction => GetClanId(clanTag, transaction));
        }

        private static long? GetClanId(string clanTag, SqlTransaction t)
        {
            const string sql =
                "select ClanId from Main.Clan where (ClanTag = @clanTag);";
            using (var cmd = new SqlCommand(sql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 5 * 60;
                cmd.Parameters.AddWithValue("@clanTag", clanTag);
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return reader.GetNonNullValue<long>(0);
                    }
                }
            }

            return null;
        }

        public Clan GetClan(ClanBaseInformation clanBaseInformation)
        {
            return GetClan(clanBaseInformation.ClanId);
        }

        public IEnumerable<Player> GetPlayersUpdateOrder(int maxPlayers, int ageHours)
        {
            return Get(transaction => GetPlayersUpdateOrder(maxPlayers, ageHours, null, transaction).ToArray());
        }

        private static IEnumerable<Player> GetPlayersUpdateOrder(int maxPlayers, int ageHours, int? minAgeHours, SqlTransaction t)
        {
            using (var cmd = new SqlCommand("Main.GetPlayerUpdateOrder", t.Connection, t))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 5 * 60;
                cmd.Parameters.AddWithValue("@max", maxPlayers);
                cmd.Parameters.AddWithValue("@ageHours", ageHours);
                if (minAgeHours.HasValue)
                {
                    cmd.Parameters.AddWithValue("@minAgeHours", minAgeHours.Value);
                }

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        yield return
                            new Player
                            {
                                Id = reader.GetNonNullValue<long>(0),
                                Name = reader.GetNonNullValue<string>(1),
                                ClanTag = reader.GetValueOrDefault<string>(2),
                                AdjustedAgeHours = reader.GetValueOrDefault<double>(3),
                                ClanId = reader.GetValueOrDefault<long?>(5),
                                ClanDelay = reader.GetValueOrDefault<double?>(7) ?? 1.0,
                                Delay = reader.GetValueOrDefault<double?>(8) ?? 1.0,
                                Platform = reader.GetNonNullValue<Platform>(11)
                            };
                    }
                }
            }
        }

        public IEnumerable<ClanBaseInformation> GetClanCalculateOrder(int ageHours)
        {
            return Get(transaction => GetClanCalculateOrder(ageHours, transaction).ToArray());
        }

        private static IEnumerable<ClanBaseInformation> GetClanCalculateOrder(int ageHours, SqlTransaction t)
        {
            using (var cmd = new SqlCommand("Main.GetClanCalculateOrder", t.Connection, t))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 5 * 60;
                cmd.Parameters.AddWithValue("@ageHours", ageHours);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        yield return new ClanBaseInformation(reader.GetNonNullValue<long>(0),
                            reader.GetNonNullValue<string>(1));
                    }
                }
            }
        }

        public Clan GetClan(long clanId)
        {
            return Get(transaction => GetClan(clanId, transaction));
        }

        private static Clan GetClan(long clanId, SqlTransaction t)
        {
            const string sql = "select Name, FlagCode, MembershipMoment, PlayerId, RankId, GamerTag, PlayerMoment, " +
                               "TotalBattles, MonthBattles, TotalWinRate, MonthWinRate, " +
                               "TotalWN8, MonthWN8, IsPatched, ClanTag, [Delay], [Enabled], IsHidden, Origin, PlayerDelay, " +
                               "TotalTier, MonthTier, PlayerPlatformId " +
                               "from [Main].[RecentClanCompositionStats] " +
                               "where (ClanId = @clanId) and (PlayerMoment is not null) " +
                               "order by PlayerId;";

            Clan clan = null;

            var playerCount = 0;

            using (var cmd = new SqlCommand(sql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 5 * 60;

                cmd.Parameters.AddWithValue("@clanId", clanId);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        ++playerCount;

                        if (playerCount == 1)
                        {
                            // Aproveita o primeiro para ler os dados gerais do clã
                            clan = new Clan(clanId, reader.GetNonNullValue<string>(14))
                            {
                                Name = reader.GetNonNullValue<string>(0),
                                Country = reader.GetValueOrDefault<string>(1) ?? string.Empty,
                                MembershipMoment = reader.GetDateTimeUtc(2),
                                Delay = reader.GetNonNullValue<double>(15),
                                Enabled = reader.GetNonNullValue<bool>(16),
                                IsHidden = reader.GetNonNullValue<bool>(17)
                            };
                        }

                        var player = new Player
                        {
                            Id = reader.GetNonNullValue<long>(3),
                            Rank = reader.GetNonNullValue<Rank>(4),
                            Name = reader.GetNonNullValue<string>(5),
                            Moment = reader.GetDateTimeUtc(6),
                            TotalBattles = reader.GetNonNullValue<int>(7),
                            MonthBattles = reader.GetNonNullValue<int>(8),
                            TotalWinRate = reader.GetNonNullValue<double>(9),
                            MonthWinRate = reader.GetNonNullValue<double>(10),
                            TotalWn8 = reader.GetNonNullValue<double>(11),
                            MonthWn8 = reader.GetNonNullValue<double>(12),
                            IsPatched = reader.GetNonNullValue<bool>(13),
                            Origin = reader.GetNonNullValue<PlayerDataOrigin>(18),
                            Delay = reader.GetNonNullValue<double>(19),
                            TotalTier = reader.GetNonNullValue<double>(20),
                            MonthTier = reader.GetNonNullValue<double>(21),
                            Platform = reader.GetNonNullValue<Platform>(22)
                        };

                        Debug.Assert(clan != null, "clan != null");
                        clan.Add(player);
                    }
                }
            }

            if (clan != null)
            {
                // Anexa a historia do clã
                const string sqlHist =
                    "select top (28*6) " +
                    "CalculationMoment, TotalMembers, ActiveMembers, TotalBattles, MonthBattles, TotalWinRate, MonthWinRate, " +
                    "TotalWN8, WN8a, WN8t15, WN8t7, Top15AvgTier, ActiveAvgTier, TotalAvgTier " +
                    "from Main.ClanDate where (ClanId = @clanId) and (CalculationMoment is not null) " +
                    "order by [Date] desc;";

                var history = new List<HistoricPoint>(28 * 6);

                using (var cmd = new SqlCommand(sqlHist, t.Connection, t))
                {
                    cmd.Parameters.AddWithValue("@clanId", clanId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var hp = new HistoricPoint
                            {
                                Moment = reader.GetDateTimeUtc(0),
                                Count = reader.GetNonNullValue<int>(1),
                                Active = reader.GetNonNullValue<int>(2),
                                TotalBattles = reader.GetNonNullValue<int>(3),
                                ActiveBattles = reader.GetNonNullValue<int>(4),
                                TotalWinRate = reader.GetNonNullValue<double>(5),
                                ActiveWinRate = reader.GetNonNullValue<double>(6),
                                TotalWn8 = reader.GetNonNullValue<double>(7),
                                ActiveWn8 = reader.GetNonNullValue<double>(8),
                                Top15Wn8 = reader.GetNonNullValue<double>(9),
                                Top7Wn8 = reader.GetNonNullValue<double>(10),
                                Top15AvgTier = reader.GetNonNullValue<double>(11),
                                ActiveAvgTier = reader.GetNonNullValue<double>(12),
                                TotalAvgTier = reader.GetNonNullValue<double>(13)
                            };

                            history.Add(hp);
                        }
                    }
                }

                clan.AttachHistory(history);

                return clan;
            }

            // Pode ser que o clã ainda não tenha tido os membros populados, ou tenha sido desabilitado
            using (var cmd = new SqlCommand(
                "select ClanTag, Name, FlagCode, [Enabled], DisabledReason from Main.Clan where (ClanId = @clanId);",
                t.Connection, t))
            {
                cmd.CommandTimeout = 5 * 60;

                cmd.Parameters.AddWithValue("@clanId", clanId);
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return new Clan(clanId, reader.GetNonNullValue<string>(0), reader.GetNonNullValue<string>(1))
                        {
                            Country = reader.GetNullableString(2),
                            Enabled = reader.GetNonNullValue<bool>(3),
                            DisabledReason = reader.GetValueOrDefault<DisabledReason?>(4) ?? DisabledReason.NotDisabled
                        };
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Get the Player (the most up to date information)
        /// </summary>
        public Player GetPlayer(long id, bool includePerformance = false, bool includeMedals = false)
        {
            return Get(transaction =>
            {
                var player = GetPlayer(id, transaction);

                if (includePerformance && (player != null))
                {
                    player.Performance = GetWn8RawStatsForPlayer(id, transaction);
                }

                if (includeMedals && (player?.Performance != null))
                {
                    FillMedals(id, player.Performance, transaction);
                }

                return player;
            });
        }

        private void FillMedals(long id, TankPlayerPeriods performance, SqlTransaction t)
        {
            const string sql = "select p.TankId, m.CategoryId, p.MedalCode, p.[Count] " +
                               "from Achievements.PlayerMedal p inner join Achievements.Medal m on (p.MedalCode = m.MedalCode) " +
                               "where (p.PlayerId = @PlayerId);";

            using (var cmd = new SqlCommand(sql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.AddWithValue("@PlayerId", id);

                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        var tankId = r.GetNonNullValue<long>(0);
                        var category = r.GetNonNullValue<Achievements.Category>(1);
                        var medal = r.GetNonNullValue<string>(2);
                        var count = r.GetNonNullValue<int>(3);

                        if (performance.All.TryGetValue(tankId, out var tank))
                        {
                            if (category == Achievements.Category.Achievements)
                            {
                                if (tank.Achievements == null)
                                {
                                    tank.Achievements = new Dictionary<string, int>();
                                }

                                tank.Achievements[medal] = count;
                            }
                            else if (category == Achievements.Category.Ribbons)
                            {
                                if (tank.Ribbons == null)
                                {
                                    tank.Ribbons = new Dictionary<string, int>();
                                }

                                tank.Ribbons[medal] = count;
                            }
                        }
                    }
                }
            }
        }

        private static Player GetPlayer(long id, SqlTransaction t)
        {
            const string sql =
                "select p.Moment, p.TotalBattles, p.MonthBattles, p.TotalWinRate, p.MonthWinRate, p.TotalWN8, p.MonthWN8, " +
                "p.IsPatched, p.Origin, " +
                "p.TotalTier, p.MonthTier, " +
                "p.Tier10TotalBattles, p.Tier10MonthBattles, p.Tier10TotalWinRate, p.Tier10MonthWinRate, p.Tier10TotalWN8, p.Tier10MonthWN8, " +
                "pp.GamerTag, pp.PlataformId, pp.[Delay], " +
                "c.ClanId, c.ClanTag, c.[Delay], cp.RankId " +
                "from [Current].Player p " +
                "inner join Main.Player pp on pp.PlayerId = p.PlayerId " +
                "left outer join [Current].ClanPlayer cp on cp.PlayerId = p.PlayerId " +
                "left outer join Main.Clan c on c.ClanId = cp.ClanId " +
                "where p.PlayerId = @playerId;";

            using (var cmd = new SqlCommand(sql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.AddWithValue("@playerId", id);

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return new Player
                        {
                            Id = id,
                            Moment = reader.GetNonNullValue<DateTime>(0).ChangeKind(DateTimeKind.Utc),
                            TotalBattles = reader.GetNonNullValue<int>(1),
                            MonthBattles = reader.GetNonNullValue<int>(2),
                            TotalWinRate = reader.GetNonNullValue<double>(3),
                            MonthWinRate = reader.GetNonNullValue<double>(4),
                            TotalWn8 = reader.GetNonNullValue<double>(5),
                            MonthWn8 = reader.GetNonNullValue<double>(6),
                            IsPatched = reader.GetNonNullValue<bool>(7),
                            Origin = reader.GetNonNullValue<PlayerDataOrigin>(8),
                            TotalTier = reader.GetNonNullValue<double>(9),
                            MonthTier = reader.GetNonNullValue<double>(10),
                            Tier10TotalBattles = reader.GetNonNullValue<int>(11),
                            Tier10MonthBattles = reader.GetNonNullValue<int>(12),
                            Tier10TotalWinRate = reader.GetNonNullValue<double>(13),
                            Tier10MonthWinRate = reader.GetNonNullValue<double>(14),
                            Tier10TotalWn8 = reader.GetNonNullValue<double>(15),
                            Tier10MonthWn8 = reader.GetNonNullValue<double>(16),
                            Name = reader.GetNonNullValue<string>(17),
                            Platform = reader.GetNonNullValue<Platform>(18),
                            Delay = reader.GetNonNullValue<double>(19),
                            ClanId = reader.GetValueOrDefault<long?>(20),
                            ClanTag = reader.GetValueOrDefault<string>(21) ?? string.Empty,
                            ClanDelay = reader.GetValueOrDefault<double?>(22) ?? 1.0,
                            Rank = reader.GetValueOrDefault<Rank?>(23) ?? Rank.Private
                        };
                    }
                }
            }

            return null;
        }

        public Player GetPlayer(long id, DateTime date, bool beforeTheDate = false, bool notPatched = false)
        {
            return Get(transaction => GetPlayer(id, date, beforeTheDate, notPatched, transaction));
        }

        private static Player GetPlayer(long id, DateTime date, bool beforeTheDate, bool notPatched, SqlTransaction t)
        {
            if (date < SqlDateTime.MinValue.Value)
            {
                // Novo jogador, faz parte de um clã mas não teve dados coletados ainda
                return null;
            }

            var sql = "select top (1) PlataformId, GamerTag, ClanTag, RankId, Moment, " +
                      "TotalBattles, MonthBattles, TotalWinRate, MonthWinRate, " +
                      "TotalWN8, MonthWN8, IsPatched, Origin, " +
                      "[Delay], ClanId, ClanDelay " +
                      "from Main.GetPlayerHistoryById(@id) where  ";
            if (beforeTheDate)
            {
                sql += "([Date] < @date)";
            }
            else
            {
                sql += "([Date] = @date)";
            }

            if (notPatched)
            {
                sql += " and (IsPatched = 0)";
            }

            sql += " order by [Date] desc;";

            using (var cmd = new SqlCommand(sql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 5 * 60;

                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@date", date);
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        if (reader.IsDBNull(4))
                        {
                            // Novo jogador, faz parte de um clã mas não teve dados coletados ainda
                            return null;
                        }

                        return new Player
                        {
                            Id = id,
                            Platform = reader.GetNonNullValue<Platform>(0),
                            Name = reader.GetNonNullValue<string>(1),
                            ClanTag = reader.GetValueOrDefault<string>(2),
                            Rank = reader.GetValueOrDefault<Rank>(3),
                            Moment = reader.GetDateTimeUtc(4),
                            TotalBattles = reader.GetNonNullValue<int>(5),
                            MonthBattles = reader.GetNonNullValue<int>(6),
                            TotalWinRate = reader.GetNonNullValue<double>(7),
                            MonthWinRate = reader.GetNonNullValue<double>(8),
                            TotalWn8 = reader.GetNonNullValue<double>(9),
                            MonthWn8 = reader.GetNonNullValue<double>(10),
                            IsPatched = reader.GetNonNullValue<bool>(11),
                            Origin = reader.GetNonNullValue<PlayerDataOrigin>(12),
                            Delay = reader.GetNonNullValue<double>(13),
                            ClanId = reader.GetValueOrDefault<long?>(14),
                            ClanDelay = reader.GetValueOrDefault<double?>(15) ?? 1.0
                        };
                    }
                }
            }

            return null;
        }

        public DataDiagnostic GetDataDiagnostic()
        {
            return Get(GetDataDiagnostic);
        }

        private static DataDiagnostic GetDataDiagnostic(SqlTransaction t)
        {
            DataDiagnostic diagnostic;

            const string sql = "select TotalPlayers, PlayersPerDay, PlayersPerHour, " +
                               "AvgPlayersPerHourLastDay, AvgPlayersPerHourLast6Hours, AvgPlayersPerHourLast2Hours, AvgPlayersPerHourLastHour, " +
                               "TotalEnabledClans, " +
                               "Last48hDelay, Last72hDelay, Last96hDelay " +
                               "from Main.NumberOfPlayersPerDay;";
            using (var cmd = new SqlCommand(sql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 5 * 60;

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        diagnostic = new DataDiagnostic
                        {
                            TotalPlayers = reader.GetNonNullValue<int>(0),
                            ScheduledPlayersPerDay = reader.GetNonNullValue<double>(1),
                            ScheduledPlayersPerHour = reader.GetNonNullValue<double>(2),
                            AvgPlayersPerHourLastDay = reader.GetNonNullValue<double>(3),
                            AvgPlayersPerHourLast6Hours = reader.GetNonNullValue<double>(4),
                            AvgPlayersPerHourLast2Hours = reader.GetNonNullValue<double>(5),
                            AvgPlayersPerHourLastHour = reader.GetNonNullValue<double>(6),
                            TotalEnabledClans = reader.GetNonNullValue<int>(7),
                            Last48HDelay = reader.GetNonNullValue<double>(8),
                            Last72HDelay = reader.GetNonNullValue<double>(9),
                            Last96HDelay = reader.GetNonNullValue<double>(10),
                        };
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
            }

            diagnostic.PlayersQueueLength = GetPlayersUpdateOrder(int.MaxValue, 24, 24, t).Count();
            diagnostic.MembershipQueueLength = GetClanMembershipUpdateOrder(int.MaxValue, 12, t).Count();
            diagnostic.CalculateQueueLength = GetClanCalculateOrder(24, t).Count();

            return diagnostic;
        }

        /// <summary>
        ///     Enumera todos os clãs habilitados
        /// </summary>
        public IEnumerable<ClanBaseInformation> GetClans()
        {
            return Get(GetClans);
        }

        private static IEnumerable<ClanBaseInformation> GetClans(SqlTransaction t)
        {
            const string sql = "select ClanId, ClanTag from Main.Clan where [Enabled] = 1 order by [ClanTag];";
            var list = new List<ClanBaseInformation>();

            using (var cmd = new SqlCommand(sql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 5 * 60;

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new ClanBaseInformation(reader.GetNonNullValue<long>(0),
                            reader.GetNonNullValue<string>(1)));
                    }
                }
            }

            return list;
        }

        public IEnumerable<Tank> GetTanks(Platform platform)
        {
            return Get(t => GetTanks(platform, t));
        }

        private static IEnumerable<Tank> GetTanks(Platform platform, SqlTransaction t)
        {
            string table;
            switch (platform)
            {
                case Platform.PC:
                    table = "PcTank";
                    break;
                case Platform.Console:
                    table = "Tank";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(platform), platform, null);
            }

            var sql = $"select TankId, [Name], ShortName, NationId, Tier, TypeId, Tag, IsPremium from Tanks.{table};";

            var list = new List<Tank>();

            using (var cmd = new SqlCommand(sql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 5 * 60;
                
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new Tank
                        {
                            Platform = platform,
                            TankId = reader.GetNonNullValue<long>(0),
                            FullName = reader.GetNonNullValue<string>(1),
                            Name = reader.GetNonNullValue<string>(2),
                            Nation = reader.GetNonNullValue<Nation>(3),
                            Tier = reader.GetNonNullValue<int>(4),
                            Type = reader.GetNonNullValue<TankType>(5),
                            Tag = reader.GetNonNullValue<string>(6),
                            IsPremium = reader.GetNonNullValue<bool>(7)
                        });
                    }
                }
            }

            return list;
        }

        /// <summary>
        ///     Se um clã existe ou não no BD
        /// </summary>
        public bool ClanExists(long clanId)
        {
            return Get(t => ClanExists(clanId, t));
        }

        private static bool ClanExists(long clanId, SqlTransaction t)
        {
            const string sql = "select 1 from Main.Clan where (ClanId = @id);";

            using (var cmd = new SqlCommand(sql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 5 * 60;

                cmd.Parameters.AddWithValue("@id", clanId);

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return true;
                    }
                }
            }

            return false;
        }


        /// <summary>
        ///     Devolve a GT a partir do nome
        /// </summary>
        public string GetPlayerNameById(Platform platform, long playerId)
        {
            return Get(t => GetPlayerNameById(platform, playerId, t));
        }

        private static string GetPlayerNameById(Platform platform, long playerId, SqlTransaction t)
        {
            const string sql = "select GamerTag from Main.Player where PlayerId = @id and PlataformId = @p;";

            using (var cmd = new SqlCommand(sql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 5 * 60;

                cmd.Parameters.AddWithValue("@id", playerId);
                cmd.Parameters.AddWithValue("@p", (int) platform);

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return reader.GetNonNullValue<string>(0);
                    }
                }
            }

            return null;
        }

        public TankPlayerPeriods GetWn8RawStatsForPlayer(long playerId, bool includeMedals = false)
        {
            return Get(t =>
            {
                var performance = GetWn8RawStatsForPlayer(playerId, t);
                if ((performance != null) && includeMedals)
                {
                    FillMedals(playerId, performance, t);
                }

                return performance;
            });
        }

        private static TankPlayerPeriods GetWn8RawStatsForPlayer(long playerId, SqlTransaction t)
        {
            var tp = new TankPlayerPeriods();

            // Overall
            const string allSql = "select TankId, Battles, Damage, Win, Frag, Spot, Def, " +
                                  "DamageAssistedTrack, DamageAssistedRadio, Shots, Hits, Piercings, ExplosionHits, CapturePoints, Losses, " +
                                  "DamageReceived, SurvivedBattles, NoDamageDirectHitsReceived, DirectHitsReceived, ExplosionHitsReceived, PiercingsReceived, " +
                                  "LastBattle, TreesCut, MaxFrags, MarkOfMastery, BattleLifeTimeSeconds, XP " +
                                  "from Performance.GetTanksStatsForWn8(@playerId, @date);";
            using (var cmd = new SqlCommand(allSql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 5 * 60;

                cmd.Parameters.AddWithValue("@playerId", playerId);
                cmd.Parameters.AddWithValue("@date", DBNull.Value);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        tp.All.Add(
                            reader.GetNonNullValue<long>(0),
                            new TankPlayerStatistics
                            {
                                Battles = reader.GetNonNullValue<long>(1),
                                DamageDealt = reader.GetNonNullValue<long>(2),
                                Wins = reader.GetNonNullValue<long>(3),
                                Kills = reader.GetNonNullValue<long>(4),
                                Spotted = reader.GetNonNullValue<long>(5),
                                DroppedCapturePoints = reader.GetNonNullValue<long>(6),
                                DamageAssistedTrack = reader.GetNonNullValue<long>(7),
                                DamageAssistedRadio = reader.GetNonNullValue<long>(8),
                                Shots = reader.GetNonNullValue<long>(9),
                                Hits = reader.GetNonNullValue<long>(10),
                                Piercings = reader.GetNonNullValue<long>(11),
                                ExplosionHits = reader.GetNonNullValue<long>(12),
                                CapturePoints = reader.GetNonNullValue<long>(13),
                                Losses = reader.GetNonNullValue<long>(14),
                                DamageReceived = reader.GetNonNullValue<long>(15),
                                SurvivedBattles = reader.GetNonNullValue<long>(16),
                                NoDamageDirectHitsReceived = reader.GetNonNullValue<long>(17),
                                DirectHitsReceived = reader.GetNonNullValue<long>(18),
                                ExplosionHitsReceived = reader.GetNonNullValue<long>(19),
                                PiercingsReceived = reader.GetNonNullValue<long>(20),
                                LastBattle = reader.GetNonNullValue<DateTime>(21).ChangeKind(DateTimeKind.Utc),
                                TreesCut = reader.GetNonNullValue<long>(22),
                                MaxFrags = reader.GetNonNullValue<long>(23),
                                MarkOfMastery = reader.GetNonNullValue<long>(24),
                                BattleLifeTimeSeconds = reader.GetNonNullValue<long>(25),
                                XP = reader.GetNonNullValue<long>(26)
                            });
                    }
                }
            }

            const string periodSql =
                "select TankId, Battles, Damage, Win, Frag, Spot, Def, " +
                "DamageAssistedTrack, DamageAssistedRadio, Shots, Hits, Piercings, ExplosionHits, CapturePoints, Losses, " +
                "DamageReceived, SurvivedBattles, NoDamageDirectHitsReceived, DirectHitsReceived, ExplosionHitsReceived, PiercingsReceived, " +
                "LastBattle, TreesCut, MaxFrags, MarkOfMastery, BattleLifeTimeSeconds, PreviousLastBattle " +
                "from Performance.GetTanksDeltaStatsForWn8(@playerId, @deltaDays);";

            // Month
            using (var cmd = new SqlCommand(periodSql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 5 * 60;

                cmd.Parameters.AddWithValue("@playerId", playerId);
                cmd.Parameters.AddWithValue("@deltaDays", 28);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        tp.Month.Add(
                            reader.GetNonNullValue<long>(0),
                            new TankPlayerStatistics
                            {
                                Battles = reader.GetNonNullValue<long>(1),
                                DamageDealt = reader.GetNonNullValue<long>(2),
                                Wins = reader.GetNonNullValue<long>(3),
                                Kills = reader.GetNonNullValue<long>(4),
                                Spotted = reader.GetNonNullValue<long>(5),
                                DroppedCapturePoints = reader.GetNonNullValue<long>(6),
                                DamageAssistedTrack = reader.GetNonNullValue<long>(7),
                                DamageAssistedRadio = reader.GetNonNullValue<long>(8),
                                Shots = reader.GetNonNullValue<long>(9),
                                Hits = reader.GetNonNullValue<long>(10),
                                Piercings = reader.GetNonNullValue<long>(11),
                                ExplosionHits = reader.GetNonNullValue<long>(12),
                                CapturePoints = reader.GetNonNullValue<long>(13),
                                Losses = reader.GetNonNullValue<long>(14),
                                DamageReceived = reader.GetNonNullValue<long>(15),
                                SurvivedBattles = reader.GetNonNullValue<long>(16),
                                NoDamageDirectHitsReceived = reader.GetNonNullValue<long>(17),
                                DirectHitsReceived = reader.GetNonNullValue<long>(18),
                                ExplosionHitsReceived = reader.GetNonNullValue<long>(19),
                                PiercingsReceived = reader.GetNonNullValue<long>(20),
                                LastBattle = reader.GetNonNullValue<DateTime>(21).ChangeKind(DateTimeKind.Utc),
                                TreesCut = reader.GetNonNullValue<long>(22),
                                MaxFrags = reader.GetNonNullValue<long>(23),
                                MarkOfMastery = reader.GetNonNullValue<long>(24),
                                BattleLifeTimeSeconds = reader.GetNonNullValue<long>(25),
                                PreviousLastBattle = reader.GetValueOrDefault<DateTime?>(26)
                                    ?.ChangeKind(DateTimeKind.Utc)
                            });
                    }
                }
            }

            // Week            
            using (var cmd = new SqlCommand(periodSql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 5 * 60;

                cmd.Parameters.AddWithValue("@playerId", playerId);
                cmd.Parameters.AddWithValue("@deltaDays", 7);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        tp.Week.Add(
                            reader.GetNonNullValue<long>(0),
                            new TankPlayerStatistics
                            {
                                Battles = reader.GetNonNullValue<long>(1),
                                DamageDealt = reader.GetNonNullValue<long>(2),
                                Wins = reader.GetNonNullValue<long>(3),
                                Kills = reader.GetNonNullValue<long>(4),
                                Spotted = reader.GetNonNullValue<long>(5),
                                DroppedCapturePoints = reader.GetNonNullValue<long>(6),
                                DamageAssistedTrack = reader.GetNonNullValue<long>(7),
                                DamageAssistedRadio = reader.GetNonNullValue<long>(8),
                                Shots = reader.GetNonNullValue<long>(9),
                                Hits = reader.GetNonNullValue<long>(10),
                                Piercings = reader.GetNonNullValue<long>(11),
                                ExplosionHits = reader.GetNonNullValue<long>(12),
                                CapturePoints = reader.GetNonNullValue<long>(13),
                                Losses = reader.GetNonNullValue<long>(14),
                                DamageReceived = reader.GetNonNullValue<long>(15),
                                SurvivedBattles = reader.GetNonNullValue<long>(16),
                                NoDamageDirectHitsReceived = reader.GetNonNullValue<long>(17),
                                DirectHitsReceived = reader.GetNonNullValue<long>(18),
                                ExplosionHitsReceived = reader.GetNonNullValue<long>(19),
                                PiercingsReceived = reader.GetNonNullValue<long>(20),
                                LastBattle = reader.GetNonNullValue<DateTime>(21).ChangeKind(DateTimeKind.Utc),
                                TreesCut = reader.GetNonNullValue<long>(22),
                                MaxFrags = reader.GetNonNullValue<long>(23),
                                MarkOfMastery = reader.GetNonNullValue<long>(24),
                                BattleLifeTimeSeconds = reader.GetNonNullValue<long>(25),
                                PreviousLastBattle = reader.GetValueOrDefault<DateTime?>(26)
                                    ?.ChangeKind(DateTimeKind.Utc)
                            });
                    }
                }
            }

            return tp;
        }

        public IEnumerable<Medal> GetMedals()
        {
            return Get(GetMedals);
        }

        private IEnumerable<Medal> GetMedals(SqlTransaction t)
        {
            const string sql = "SELECT [MedalCode], [Name], [Description], [HeroInformation], [Condition], " +
                               "[CategoryId], [TypeId], [SectionId] " +
                               "FROM [Achievements].[Medal];";

            var medals = new List<Medal>();

            using (var cmd = new SqlCommand(sql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;

                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        medals.Add(new Medal()
                        {
                            Platform = Platform.Console,
                            Code = r.GetNonNullValue<string>(0),
                            Name = r.GetNonNullValue<string>(1),
                            Description = r.GetValueOrDefault<string>(2),
                            HeroInformation = r.GetValueOrDefault<string>(3),
                            Condition = r.GetValueOrDefault<string>(4),
                            Category = r.GetNonNullValue<Category>(5),
                            Type = r.GetNonNullValue<Achievements.Type>(6),
                            Section = r.GetNonNullValue<Section>(7)
                        });
                    }
                }
            }

            return medals;
        }

        public Wn8ExpectedValues GetWn8ExpectedValues()
        {
            return Get(GetWn8ExpectedValues);
        }

        private static Wn8ExpectedValues GetWn8ExpectedValues(SqlTransaction t)
        {
            const string sql =
                "SELECT [TankId], [ShortName], [Tier], [TypeId], [NationId], [IsPremium], [Tag], [Name], " +
                "[Def], [Frag], [Spot], [Damage], [WinRate], " +
                "[Origin], [Source], [Version], [Date] " +
                "FROM [Tanks].[CurrentWn8Expected];";

            using (var cmd = new SqlCommand(sql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 5 * 60;

                Wn8ExpectedValues ev = null;
                var tvs = new List<Wn8TankExpectedValues>(600);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (ev == null)
                        {
                            ev = new Wn8ExpectedValues
                            {
                                Date = reader.GetNonNullValue<DateTime>(16),
                                Source = reader.GetNonNullValue<string>(14),
                                Version = reader.GetNonNullValue<string>(15)
                            };
                        }

                        tvs.Add(new Wn8TankExpectedValues
                        {
                            Platform = Platform.Console,

                            TankId = reader.GetNonNullValue<long>(0),
                            Name = reader.GetNonNullValue<string>(1),
                            Tier = reader.GetNonNullValue<int>(2),
                            Type = reader.GetNonNullValue<TankType>(3),
                            Nation = reader.GetNonNullValue<Nation>(4),
                            IsPremium = reader.GetNonNullValue<bool>(5),
                            Tag = reader.GetNonNullValue<string>(6),
                            FullName = reader.GetNonNullValue<string>(7),

                            Def = reader.GetNonNullValue<double>(8),
                            Frag = reader.GetNonNullValue<double>(9),
                            Spot = reader.GetNonNullValue<double>(10),
                            Damage = reader.GetNonNullValue<double>(11),
                            WinRate = reader.GetNonNullValue<double>(12),

                            Origin = reader.GetNonNullValue<Wn8TankExpectedValuesOrigin>(13)
                        });
                    }
                }

                if (ev != null)
                {
                    ev.AllTanks = tvs.ToArray();
                }

                return ev;
            }
        }

        /// <summary>
        /// Enumerate basic information from all clans
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Clan> EnumClans()
        {
            return Get(EnumClans);
        }

        private static IEnumerable<Clan> EnumClans(SqlTransaction t)
        {
            const string sql = "SELECT ClanId, ClanTag, [Name], FlagCode, [Enabled], Delay, IsHidden, DisabledReason " +
                               "FROM Main.Clan;";

            var l = new List<Clan>();

            using (var cmd = new SqlCommand(sql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 5 * 60;

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        l.Add(new Clan(reader.GetNonNullValue<long>(0), reader.GetNonNullValue<string>(1),
                            reader.GetNullableString(2))
                        {
                            Country = reader.GetNullableString(3),
                            Enabled = reader.GetNonNullValue<bool>(4),
                            Delay = reader.GetNonNullValue<double>(5),
                            IsHidden = reader.GetNonNullValue<bool>(6),
                            DisabledReason = reader.GetNonNullValue<DisabledReason>(7)
                        });
                    }
                }

                return l;
            }
        }
    }
}