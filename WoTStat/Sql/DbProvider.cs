using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Linq;
using log4net;
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

        public IEnumerable<ClanPlataform> GetClanMembershipUpdateOrder(int maxClans, int ageHours)
        {
            return Get(transaction => GetClanMembershipUpdateOrder(maxClans, ageHours, transaction).ToArray());
        }

        private static IEnumerable<ClanPlataform> GetClanMembershipUpdateOrder(int maxClans, int ageHours, SqlTransaction t)
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
                        yield return new ClanPlataform(reader.GetNonNullValue<Platform>(0), reader.GetNonNullValue<long>(4),
                            reader.GetNonNullValue<string>(1));
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
            const string query = "select top (1) PlayerId from Main.Player where (PlataformId = @plataformId) and (GamerTag = @gamerTag);";
            using (var cmd = new SqlCommand(query, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 15;
                cmd.Parameters.AddWithValue("@plataformId", (int) platform);
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

        public IEnumerable<TankReference> GetTanksReferences(Platform platform, 
            DateTime? date = null, long? tankId = null, bool includeMoe = true, bool includeHistogram = true, bool includeLeaders = true)
        {
            return Get(transaction => GetTanksReferences(platform, date, tankId, includeMoe, includeHistogram, includeLeaders, transaction).ToArray());
        }

        /// <summary>
        /// Enumerate all tanks
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Tank> EnumTanks(Platform platform)
        {
            return Get(transaction => EnumTanks(platform, transaction).ToArray());
        }

        private static IEnumerable<Tank> EnumTanks(Platform platform, SqlTransaction t)
        {
            var tanks = new List<Tank>(600);

            const string sql = "select TankId, [Name], ShortName, NationId, Tier, TypeId, Tag, IsPremium from Tanks.Tank where (PlataformId = @plataformId) and (IsJoke = 0);";
            using (var cmd = new SqlCommand(sql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 5 * 60;
                cmd.Parameters.AddWithValue("@plataformId", (int)platform);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var tank = new Tank
                        {
                            Plataform = platform,
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
        /// Retrieve the leaderboard for a given tank
        /// </summary>
        public IEnumerable<Leader> GetLeaderboard(Platform platform, long tankId, int top = 25, string flagCode = null, int skip = 0)
        {
            return Get(t => GetLeaderboard(platform, tankId, top, flagCode, skip, t));
        }

        private static IEnumerable<Leader> GetLeaderboard(Platform platform, long tankId, int top, string flagCode, int skip, SqlTransaction t)
        {
            var list = new List<Leader>(top);

            using (var cmd = new SqlCommand("Performance.GetTopPlayersAll", t.Connection, t))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@plataformId", (int)platform);
                cmd.Parameters.AddWithValue("@tankId", tankId);
                cmd.Parameters.AddWithValue("@top", top);
                if (!string.IsNullOrWhiteSpace(flagCode))
                {
                    cmd.Parameters.AddWithValue("@flagCode", flagCode);
                }

                using (var reader = cmd.ExecuteReader())
                {
                    var leaders = new List<Leader>();
                    var order = 1;
                    while (reader.Read())
                    {
                        list.Add(
                            new Leader
                            {
                                Order = order++,
                                Plataform = platform,
                                TankId = tankId,
                                PlayerId = reader.GetNonNullValue<long>(1),
                                GamerTag = reader.GetNonNullValue<string>(2),
                                ClanId = reader.GetNonNullValue<long>(3),
                                ClanTag = reader.GetNonNullValue<string>(4),
                                ClanFlag = reader.GetValueOrDefault<string>(5),
                                LastBattle = reader.GetNonNullValue<DateTime>(6).ChangeKind(DateTimeKind.Utc),
                                BattleTime = TimeSpan.FromSeconds(reader.GetNonNullValue<long>(7)),
                                Battles = reader.GetNonNullValue<long>(8),
                                TotalDamage = (double)reader.GetNonNullValue<decimal>(9),
                                DamageAssisted = (double)reader.GetNonNullValue<decimal>(10),
                                Kills = (double)reader.GetNonNullValue<decimal>(11),
                                MaxKills = reader.GetNonNullValue<long>(12),
                                Spotted = (double)reader.GetNonNullValue<decimal>(13),
                                Date = reader.GetNonNullValue<DateTime>(14).ChangeKind(DateTimeKind.Utc),
                                Name = reader.GetNonNullValue<string>(15),
                                FullName = reader.GetNonNullValue<string>(16),
                                IsPremium = reader.GetNonNullValue<bool>(17),
                                Tag = reader.GetNonNullValue<string>(18),
                                Tier = reader.GetNonNullValue<int>(19),
                                Type = reader.GetNonNullValue<TankType>(20),
                                Nation = reader.GetNonNullValue<Nation>(21)
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

        private static IEnumerable<TankPlayerStatistics> GetPlayerHistoryByTank(Platform platform, long playerId, long tankId, SqlTransaction t)
        {
            var l = new List<TankPlayerStatistics>();

            const string sql = "select TankId, Battles, Damage, Win, Frag, Spot, Def, " +
                                  "DamageAssistedTrack, DamageAssistedRadio, Shots, Hits, Piercings, ExplosionHits, CapturePoints, Losses, " +
                                  "DamageReceived, SurvivedBattles, NoDamageDirectHitsReceived, DirectHitsReceived, ExplosionHitsReceived, PiercingsReceived, " +
                                  "LastBattle, TreesCut, MaxFrags, MarkOfMastery, BattleLifeTimeSeconds " +
                                  "from Performance.GetTanksStatsForWn8Hist(@plataformId, @playerId, @tankId) " +
                                  "order by LastBattle desc;";
            using (var cmd = new SqlCommand(sql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.AddWithValue("@plataformId", (int)platform);
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
            const string sql = "select ClanId, ClanTag, Moment, TotalBattles, MonthBattles, TotalWn8, MonthWn8 from Main.GetPlayerHistoryById(@playerId) order by moment desc;";

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

        private static IEnumerable<TankReference> GetTanksReferences(Platform platform, 
            DateTime? date, long? tankId, bool includeMoe, bool includeHistogram, bool includeLeaders, SqlTransaction t)
        {
            var references = new Dictionary<long, TankReference>();

            // Get the overall data
            using (var cmd = new SqlCommand("Performance.GetTanksReferences", t.Connection, t))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 5 * 60;
                cmd.Parameters.AddWithValue("@period", ReferencePeriod.All.ToString());
                cmd.Parameters.AddWithValue("@plataformId", (int) platform);
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
                            Plataform = platform,
                            Name = reader.GetNonNullValue<string>(4),
                            Tier = reader.GetNonNullValue<int>(5),
                            Type = reader.GetNonNullValue<TankType>(6),
                            Nation = reader.GetNonNullValue<Nation>(8),
                            IsPremium = reader.GetNonNullValue<bool>(10),
                            Tag = reader.GetNonNullValue<string>(11),
                            Spotted = reader.GetNonNullValue<double>(12),
                            DamageAssistedTrack = reader.GetNonNullValue<double>(13),
                            DamageAssistedRadio = reader.GetNonNullValue<double>(14),
                            CapturePoints = reader.GetNonNullValue<double>(15),
                            DroppedCapturePoints = reader.GetNonNullValue<double>(16),
                            TotalBattles = reader.GetNonNullValue<long>(17),
                            TotalPlayers = reader.GetNonNullValue<int>(18),
                            TotalWins = reader.GetNonNullValue<long>(19),
                            TotalLosses = reader.GetNonNullValue<long>(20),
                            Kills = reader.GetNonNullValue<double>(21),
                            TotalSurvivedBattles = reader.GetNonNullValue<long>(22),
                            DamageDealt = reader.GetNonNullValue<double>(23),
                            DamageReceived = reader.GetNonNullValue<double>(24),
                            TotalTime = TimeSpan.FromHours(reader.GetNonNullValue<double>(25)),
                            TreesCut = reader.GetNonNullValue<double>(26),
                            MaxKills = reader.GetNonNullValue<double>(27),
                            MarkOfMastery = reader.GetNonNullValue<double>(28),
                            Hits = reader.GetNonNullValue<double>(29),
                            NoDamageDirectHitsReceived = reader.GetNonNullValue<double>(30),
                            ExplosionHits = reader.GetNonNullValue<double>(31),
                            Piercings = reader.GetNonNullValue<double>(32),
                            Shots = reader.GetNonNullValue<double>(33),
                            ExplosionHitsReceived = reader.GetNonNullValue<double>(34),
                            XP = reader.GetNonNullValue<double>(35),
                            DirectHitsReceived = reader.GetNonNullValue<double>(36),
                            PiercingReceived = reader.GetNonNullValue<double>(37),
                            FullName = reader.GetNonNullValue<string>(38)
                        };
                        references.Add(tr.TankId, tr);
                    }
                }
            }

            // Obtém os MoEs na data
            var moes = includeMoe ? GetMoe(platform, date, tankId, t).ToDictionary(m => m.TankId) : new Dictionary<long, TankMoe>();

            // WN8, Histograms and Leaders
            foreach (var tr in references.Values)
            {
                if (moes.TryGetValue(tr.TankId, out var moe))
                {
                    tr.MoeHighMark = moe.HighMarkDamage;
                }

                // WN8
                const string sqlWn8 = "select Def, Frag, Spot, Damage, WinRate, Origin from Tanks.CurrentWn8Expected where TankId = @tankId;";
                using (var cmd = new SqlCommand(sqlWn8, t.Connection, t))
                {
                    cmd.Parameters.AddWithValue("@tankId", tr.TankId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            tr.Wn8Values = new Wn8TankExpectedValues
                            {
                                Plataform = tr.Plataform,
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

                            tr.AverageWn8 = tr.Wn8Values.GetWn8(tr.DamageDealt, tr.WinRatio, tr.Kills, tr.Spotted, tr.DroppedCapturePoints);
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
                    // Obtem histogramas se o tanque for significativo
                    Log.DebugFormat("Obtendo histogramas do tank {0}.{1}.{2}...", platform, tr.TankId, tr.Name);

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
                            cmd.Parameters.AddWithValue("@plataformId", (int)tr.Plataform);
                            cmd.Parameters.AddWithValue("@tankId", tr.TankId);
                            cmd.Parameters.Add(new SqlParameter("@metric", SqlDbType.VarChar, 50) { Value = metric });
                            cmd.Parameters.AddWithValue("@levels", 50);

                            var hist = new Histogram();
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    hist.Add(
                                        (double)reader.GetNonNullValue<decimal>(0),
                                        (double)reader.GetNonNullValue<decimal>(1),
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

                // Obtem lideres
                if ((tr.TotalPlayers > 100) && (tr.TotalBattles > 1000) && (tr.Tier >= 5) && includeLeaders)
                {
                    tr.Leaders = GetLeaderboard(tr.Plataform, tr.TankId, 25, null, 0, t).ToArray();
                }
            }

            // Get Last Month data
            using (var cmd = new SqlCommand("Performance.GetTanksReferences", t.Connection, t))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 5 * 60;
                cmd.Parameters.AddWithValue("@period", ReferencePeriod.Month.ToString());
                cmd.Parameters.AddWithValue("@plataformId", (int)platform);
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
                        var tr = new TankReferenceBasic()
                        {
                            TankId = reader.GetNonNullValue<long>(2),
                            Plataform = platform,
                            Name = reader.GetNonNullValue<string>(4),
                            Tier = reader.GetNonNullValue<int>(5),
                            Type = reader.GetNonNullValue<TankType>(6),
                            Nation = reader.GetNonNullValue<Nation>(8),
                            IsPremium = reader.GetNonNullValue<bool>(10),
                            Tag = reader.GetNonNullValue<string>(11),
                            Spotted = reader.GetNonNullValue<double>(12),
                            DamageAssistedTrack = reader.GetNonNullValue<double>(13),
                            DamageAssistedRadio = reader.GetNonNullValue<double>(14),
                            CapturePoints = reader.GetNonNullValue<double>(15),
                            DroppedCapturePoints = reader.GetNonNullValue<double>(16),
                            TotalBattles = reader.GetNonNullValue<long>(17),
                            TotalPlayers = reader.GetNonNullValue<int>(18),
                            TotalWins = reader.GetNonNullValue<long>(19),
                            TotalLosses = reader.GetNonNullValue<long>(20),
                            Kills = reader.GetNonNullValue<double>(21),
                            TotalSurvivedBattles = reader.GetNonNullValue<long>(22),
                            DamageDealt = reader.GetNonNullValue<double>(23),
                            DamageReceived = reader.GetNonNullValue<double>(24),
                            TotalTime = TimeSpan.FromHours(reader.GetNonNullValue<double>(25)),
                            TreesCut = reader.GetNonNullValue<double>(26),
                            MaxKills = reader.GetNonNullValue<double>(27),
                            MarkOfMastery = reader.GetNonNullValue<double>(28),
                            Hits = reader.GetNonNullValue<double>(29),
                            NoDamageDirectHitsReceived = reader.GetNonNullValue<double>(30),
                            ExplosionHits = reader.GetNonNullValue<double>(31),
                            Piercings = reader.GetNonNullValue<double>(32),
                            Shots = reader.GetNonNullValue<double>(33),
                            ExplosionHitsReceived = reader.GetNonNullValue<double>(34),
                            XP = reader.GetNonNullValue<double>(35),
                            DirectHitsReceived = reader.GetNonNullValue<double>(36),
                            PiercingReceived = reader.GetNonNullValue<double>(37),
                            FullName = reader.GetNonNullValue<string>(38)
                        };

                        var all = references[tr.TankId];
                        tr.AverageWn8 = all.Wn8Values.GetWn8(tr.DamageDealt, tr.WinRatio, tr.Kills, tr.Spotted, tr.DroppedCapturePoints);

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
                return (long)o;
            }
        }

        public IEnumerable<TankMoe> GetMoe(Platform platform, DateTime? date = null, long? tankId = null)
        {
            return Get(transaction => GetMoe(platform, date, tankId, transaction).ToArray());
        }

        private static IEnumerable<TankMoe> GetMoe(Platform platform, DateTime? date, long? tankId, SqlTransaction t)
        {
            using (var cmd = new SqlCommand("Performance.GetMoE", t.Connection, t))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 5 * 60;
                cmd.Parameters.AddWithValue("@methodId", 4); // Metodos dos Percentis
                cmd.Parameters.AddWithValue("@plataformId", (int) platform);
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
                            Plataform = platform,
                            Name = reader.GetNonNullValue<string>(2),
                            Tier = reader.GetNonNullValue<int>(3),
                            Type = reader.GetNonNullValue<TankType>(4),
                            Nation = reader.GetNonNullValue<Nation>(6),
                            IsPremium = reader.GetNonNullValue<bool>(8),
                            Tag = reader.GetNonNullValue<string>(9),
                            HighMarkDamage = reader.GetNonNullValue<double>(10),
                            NumberOfDates = reader.GetNonNullValue<int>(11),
                            NumberOfBattles = reader.GetNonNullValue<long>(12),
                            TankId = reader.GetNonNullValue<long>(13),
                            FullName = reader.GetNonNullValue<string>(14)
                        };
                    }
                }
            }
        }

        public Clan GetClan(Platform platform, string clanTag)
        {
            var clanId = GetClanId(platform, clanTag);
            if (!clanId.HasValue)
            {
                return null;
            }

            return GetClan(platform, clanId.Value);
        }

        private long? GetClanId(Platform platform, string clanTag)
        {
            return Get(transaction => GetClanId(platform, clanTag, transaction));
        }

        private static long? GetClanId(Platform platform, string clanTag, SqlTransaction t)
        {
            const string sql = "select ClanId from Main.Clan where (ClanTag = @clanTag) and (PlataformId = @plataform);";
            using (var cmd = new SqlCommand(sql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 5 * 60;
                cmd.Parameters.AddWithValue("@clanTag", clanTag);
                cmd.Parameters.AddWithValue("@plataform", (int) platform);
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

        public Clan GetClan(ClanPlataform clanPlataform)
        {
            return GetClan(clanPlataform.Plataform, clanPlataform.ClanId);
        }

        public IEnumerable<Player> GetPlayersUpdateOrder(int maxPlayers, int ageHours)
        {
            return Get(transaction => GetPlayersUpdateOrder(maxPlayers, ageHours, null, null, transaction).ToArray());
        }

        private static IEnumerable<Player> GetPlayersUpdateOrder(int maxPlayers, int ageHours, int? minAgeHours, bool? shouldPurgePriorityPlayers,
            SqlTransaction t)
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

                if (shouldPurgePriorityPlayers.HasValue)
                {
                    cmd.Parameters.AddWithValue("@shouldPurgePriorityPlayers", shouldPurgePriorityPlayers.Value);
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
                                Plataform = reader.GetNonNullValue<Platform>(2),
                                ClanTag = reader.GetValueOrDefault<string>(3),
                                AdjustedAgeHours = reader.GetValueOrDefault<double>(4),
                                ClanId = reader.GetValueOrDefault<long?>(6),
                                ClanDelay = reader.GetValueOrDefault<double?>(8) ?? 1.0,
                                Delay = reader.GetValueOrDefault<double?>(9) ?? 1.0
                            };
                    }
                }
            }
        }

        public IEnumerable<ClanPlataform> GetClanCalculateOrder(int ageHours)
        {
            return Get(transaction => GetClanCalculateOrder(ageHours, transaction).ToArray());
        }

        private static IEnumerable<ClanPlataform> GetClanCalculateOrder(int ageHours, SqlTransaction t)
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
                        yield return new ClanPlataform(reader.GetNonNullValue<Platform>(0), reader.GetNonNullValue<long>(1),
                            reader.GetNonNullValue<string>(2));
                    }
                }
            }
        }

        public Clan GetClan(Platform platform, long clanId)
        {
            return Get(transaction => GetClan(platform, clanId, transaction));
        }

        /// <summary>
        ///     Retorna uma lista de clãs a serem adicionados ao sistema
        /// </summary>
        /// <remarks>
        ///     O id dos clãs estará vazio, naturalmente, pois será localizado chamando-se a API da WG
        /// </remarks>
        public IEnumerable<Clan> GetClansToAdd()
        {
            return Get(GetClansToAdd);
        }

        private static IEnumerable<Clan> GetClansToAdd(SqlTransaction t)
        {
            var list = new List<Clan>();

            const string sql = "select PlataformId, ClanTag, FlagCode from Main.ClanRequest;";
            using (var cmd = new SqlCommand(sql, t.Connection, t))
            {
                cmd.CommandTimeout = 5 * 60;
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new Clan(reader.GetNonNullValue<Platform>(0), 0, reader.GetNonNullValue<string>(1))
                        {
                            Country = reader.GetNullableString(2)
                        });
                    }
                }
            }

            return list;
        }

        private static Clan GetClan(Platform platform, long clanId, SqlTransaction t)
        {
            const string sql = "select Name, FlagCode, MembershipMoment, PlayerId, RankId, GamerTag, PlayerMoment, " +
                               "TotalBattles, MonthBattles, WeekBattles, TotalWinRate, MonthWinRate, WeekWinRate, " +
                               "TotalWN8, MonthWN8, WeekWN8, IsPatched, ClanTag, [Delay], [Enabled], IsHidden, Origin, PlayerDelay, " +
                               "TotalTier, MonthTier " +
                               "from [Main].[RecentClanCompositionStats] " +
                               "where (PlataformId = @plataformId) and (ClanId = @clanId) and (PlayerMoment is not null) " +
                               "order by PlayerId;";

            Clan clan = null;

            var playerCount = 0;

            using (var cmd = new SqlCommand(sql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 5 * 60;

                cmd.Parameters.AddWithValue("@plataformId", (int) platform);
                cmd.Parameters.AddWithValue("@clanId", clanId);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        ++playerCount;

                        if (playerCount == 1)
                        {
                            // Aproveita o primeiro para ler os dados gerais do clã
                            clan = new Clan(platform, clanId, reader.GetNonNullValue<string>(17))
                            {
                                Name = reader.GetNonNullValue<string>(0),
                                Country = reader.GetValueOrDefault<string>(1) ?? string.Empty,
                                MembershipMoment = reader.GetDateTimeUtc(2),
                                Delay = reader.GetNonNullValue<double>(18),
                                Enabled = reader.GetNonNullValue<bool>(19),
                                IsHidden = reader.GetNonNullValue<bool>(20)
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
                            //WeekBattles = reader.GetNonNullValue<int>(9),
                            TotalWinRate = reader.GetNonNullValue<double>(10),
                            MonthWinRate = reader.GetNonNullValue<double>(11),
                            //WeekWinRate = reader.GetNonNullValue<double>(12),
                            TotalWn8 = reader.GetNonNullValue<double>(13),
                            MonthWn8 = reader.GetNonNullValue<double>(14),
                            //WeekWn8 = reader.GetNonNullValue<double>(15),
                            IsPatched = reader.GetNonNullValue<bool>(16),
                            Origin = reader.GetNonNullValue<PlayerDataOrigin>(21),
                            Delay = reader.GetNonNullValue<double>(22),
                            TotalTier = reader.GetNonNullValue<double>(23),
                            MonthTier = reader.GetNonNullValue<double>(24)
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
                    "CalculationMoment, TotalMembers, ActiveMembers, TotalBattles, MonthBattles, TotalWinRate, MonthWinrate, " +
                    "TotalWN8, WN8a, WN8t15, WN8t7, Top15AvgTier, ActiveAvgTier, TotalAvgTier " +
                    "from Main.ClanDate where (ClanTag = @clanTag) and (PlataformId = @plataformId) and (CalculationMoment is not null) " +
                    "order by [Date] desc;";

                var history = new List<HistoricPoint>(28 * 6);

                using (var cmd = new SqlCommand(sqlHist, t.Connection, t))
                {
                    cmd.Parameters.AddWithValue("@clanTag", clan.ClanTag);
                    cmd.Parameters.AddWithValue("@plataformId", (int) platform);
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
            using (var cmd = new SqlCommand("select ClanTag, Name, FlagCode, [Enabled], DisabledReason from Main.Clan where (PlataformId = @plataformId) and (ClanId = @clanId);",
                t.Connection, t))
            {
                cmd.CommandTimeout = 5 * 60;

                cmd.Parameters.AddWithValue("@clanId", clanId);
                cmd.Parameters.AddWithValue("@plataformId", (int) platform);
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return new Clan(platform, clanId, reader.GetNonNullValue<string>(0), reader.GetNonNullValue<string>(1))
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
        public Player GetPlayer(long id, bool includePerformance = false)
        {
            return Get(transaction =>
            {
                var player = GetPlayer(id, transaction);

                if (includePerformance && (player != null))
                {
                    player.Performance = GetWn8RawStatsForPlayer(player.Plataform, id, transaction);
                }

                return player;
            });
        }

        private static Player GetPlayer(long id, SqlTransaction t)
        {
            const string sql = "select p.Moment, p.TotalBattles, p.MonthBattles, p.TotalWinRate, p.MonthWinRate, p.TotalWN8, p.MonthWN8, " +
                               "p.IsPatched, p.Origin, " +
                               "p.TotalTier, p.MonthTier, " +
                               "p.Tier10TotalBattles, p.Tier10MonthBattles, p.Tier10TotalWinRate, p.Tier10MonthWinRate, p.Tier10TotalWN8, p.Tier10MonthWN8, " +
                               "pp.GamerTag, pp.PlataformId, pp.[Delay], " +
                               "c.ClanId, c.ClanTag, c.[Delay], cp.RankId " +
                               "from [Current].Player p " +
                               "inner join Main.Player pp on pp.PlayerId = p.PlayerId " +
                               "left outer join [Current].ClanPlayer cp on cp.PlayerId = p.PlayerId " +
                               "left outer join Main.Clan c on c.ClanId = cp.ClanId and c.PlataformId = cp.PlataformId " +
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
                            Plataform = reader.GetNonNullValue<Platform>(18),
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
                            Plataform = reader.GetNonNullValue<Platform>(0),
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

            diagnostic.PlayersQueueLenght = GetPlayersUpdateOrder(int.MaxValue, 24, 24, false, t).Count();
            diagnostic.MembershipQueueLenght = GetClanMembershipUpdateOrder(int.MaxValue, 12, t).Count();
            diagnostic.CalculateQueueLenght = GetClanCalculateOrder(24, t).Count();

            return diagnostic;
        }

        /// <summary>
        ///     Enumera todos os clãs habilitados
        /// </summary>
        public IEnumerable<ClanPlataform> GetClans()
        {
            return Get(GetClans);
        }

        private static IEnumerable<ClanPlataform> GetClans(SqlTransaction t)
        {
            const string sql = "select PlataformId, ClanId, ClanTag from Main.Clan where [Enabled] = 1 order by [ClanTag];";
            var list = new List<ClanPlataform>();

            using (var cmd = new SqlCommand(sql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 5 * 60;

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new ClanPlataform(reader.GetNonNullValue<Platform>(0), reader.GetNonNullValue<long>(1), reader.GetNonNullValue<string>(2)));
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
            const string sql = "select TankId, [Name], ShortName, NationId, Tier, TypeId, Tag, IsPremium " +
                               "from Tanks.Tank " +
                               "where PlataformId = @plataformId;";

            var list = new List<Tank>();

            using (var cmd = new SqlCommand(sql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 5 * 60;
                cmd.Parameters.AddWithValue("@plataformId", (int) platform);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new Tank
                        {
                            Plataform = platform,
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
        public bool ClanExists(Platform platform, long clanId)
        {
            return Get(t => ClanExists(platform, clanId, t));
        }

        private static bool ClanExists(Platform platform, long clanId, SqlTransaction t)
        {
            const string sql = "select 1 from Main.Clan where (PlataformId = @p) and (ClanId = @id);";

            using (var cmd = new SqlCommand(sql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 5 * 60;

                cmd.Parameters.AddWithValue("@p", (int) platform);
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

        public TankPlayerPeriods GetWn8RawStatsForPlayer(Platform platform, long playerId)
        {
            return Get(t => GetWn8RawStatsForPlayer(platform, playerId, t));
        }

        private static TankPlayerPeriods GetWn8RawStatsForPlayer(Platform platform, long playerId, SqlTransaction t)
        {
            var tp = new TankPlayerPeriods();

            // Overall
            const string allSql = "select TankId, Battles, Damage, Win, Frag, Spot, Def, " +
                                  "DamageAssistedTrack, DamageAssistedRadio, Shots, Hits, Piercings, ExplosionHits, CapturePoints, Losses, " +
                                  "DamageReceived, SurvivedBattles, NoDamageDirectHitsReceived, DirectHitsReceived, ExplosionHitsReceived, PiercingsReceived, " +
                                  "LastBattle, TreesCut, MaxFrags, MarkOfMastery, BattleLifeTimeSeconds, XP " +
                                  "from Performance.GetTanksStatsForWn8(@plataformId, @playerId, @date);";
            using (var cmd = new SqlCommand(allSql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 5 * 60;

                cmd.Parameters.AddWithValue("@plataformId", (int) platform);
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
                "from Performance.GetTanksDeltaStatsForWn8(@plataformId, @playerId, @deltaDays);";

            // Month
            using (var cmd = new SqlCommand(periodSql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 5 * 60;

                cmd.Parameters.AddWithValue("@plataformId", (int) platform);
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
                                PreviousLastBattle = reader.GetValueOrDefault<DateTime?>(26)?.ChangeKind(DateTimeKind.Utc)
                            });
                    }
                }
            }

            // Week            
            using (var cmd = new SqlCommand(periodSql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 5 * 60;

                cmd.Parameters.AddWithValue("@plataformId", (int) platform);
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
                                PreviousLastBattle = reader.GetValueOrDefault<DateTime?>(26)?.ChangeKind(DateTimeKind.Utc)
                            });
                    }
                }
            }

            return tp;
        }

        public Wn8ExpectedValues GetWn8ExpectedValues(Platform platform)
        {
            return Get(t => GetWn8ExpectedValues(platform, t));
        }

        private static Wn8ExpectedValues GetWn8ExpectedValues(Platform platform, SqlTransaction t)
        {
            const string sql = "SELECT [TankId], [ShortName], [Tier], [TypeId], [NationId], [IsPremium], [Tag], [Name], " +
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
                            Plataform = platform,

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
    }
}