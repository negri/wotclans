using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using log4net;
using Negri.Wot.Achievements;
using Negri.Wot.Tanks;
using Negri.Wot.WgApi;
using Tank = Negri.Wot.WgApi.Tank;

namespace Negri.Wot.Sql
{
    /// <summary>
    ///     Salva clãs no Sql
    /// </summary>
    public class DbRecorder : DataAccessBase
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(DbRecorder));


        public DbRecorder(string connectionString) : base(connectionString)
        {
        }

        /// <summary>
        ///     Seta um clã inteiro no BD
        /// </summary>
        public void Set(Clan clan, bool onlyMembership = false)
        {
            Log.DebugFormat("Setting clan [{0}] in DB...", clan.ClanTag);
            var sw = Stopwatch.StartNew();
            Execute(transaction => { Set(clan, onlyMembership, transaction); });
            Log.DebugFormat("Set clan [{0}] in {1}.", clan.ClanTag, sw.Elapsed);
        }

        private static void Set(Clan clan, bool onlyMembership, SqlTransaction t)
        {
            var date = clan.Date;

            var databaseClanTag = GetClanTag(clan.ClanId, t);
            if (string.IsNullOrWhiteSpace(databaseClanTag))
            {
                throw new ApplicationException(
                    $"Should exist in Main.Clan the clan {clan.ClanId}.{clan.ClanTag}!");
            }

            if (databaseClanTag != clan.ClanTag)
            {
                // verifica se o novo nome não vai conflitar com algo que ainda não tenha sido refletido.
                long? otherClanId = null;
                const string sqlCheckName =
                    "select ClanId from Main.Clan where (ClanTag = @clanTag);";
                using (var cmd = new SqlCommand(sqlCheckName, t.Connection, t))
                {
                    cmd.CommandTimeout = 5 * 60;
                    cmd.Parameters.AddWithValue("@clanTag", clan.ClanTag);
                    var o = cmd.ExecuteScalar();
                    if (o != null)
                    {
                        var existingId = (long)o;
                        if (existingId != clan.ClanId)
                        {
                            // O novo nome colide com algum outro...
                            otherClanId = existingId;
                            Log.WarnFormat("The new tag of {0}.{1}.{2} crashes with {0}.{1}.{3}...", clan.ClanTag, clan.Platform, clan.ClanId, existingId);
                        }
                    }
                }

                if (otherClanId.HasValue)
                {
                    // O novo nome colide com algum outro no BD. É um clã ativo?
                    bool isEnabled;
                    const string sqlIsEnabled =
                        "select [Enabled] from Main.Clan where (ClanId = @clanId);";
                    using (var cmd = new SqlCommand(sqlIsEnabled, t.Connection, t))
                    {
                        cmd.CommandTimeout = 5 * 60;
                        cmd.Parameters.AddWithValue("@clanId", otherClanId.Value);
                        isEnabled = (bool)cmd.ExecuteScalar();
                    }
                    if (isEnabled)
                    {
                        // Basta esperar que o anterior seja atualizado para seja lá o que for
                        Log.Warn("The conflicted tag is still in use. Just wait until it get changed.");
                        return;
                    }

                    // Troca o tag do antigo para algo que não dê conflito
                    const string sqlChangeDisabled =
                        "update Main.Clan set ClanTag = @clanTag where (ClanId = @clanId);";
                    using (var cmd = new SqlCommand(sqlChangeDisabled, t.Connection, t))
                    {
                        cmd.CommandTimeout = 5 * 60;

                        var rand = new Random();
                        var disabledName = "+" + rand.Next(0, 10000).ToString(CultureInfo.InvariantCulture);

                        Log.WarnFormat("Changing tag of innactive clan {0}.{2} to {2}",
                            clan.ClanTag, otherClanId.Value, disabledName);

                        cmd.Parameters.AddWithValue("@clanTag", disabledName);
                        cmd.Parameters.AddWithValue("@clanId", otherClanId.Value);

                        cmd.ExecuteNonQuery();
                    }
                }

                // O clã mudou de tag...
                const string sqlUpdClan =
                    "update Main.Clan set ClanTag = @clanTag where (ClanId = @clanId);";
                using (var cmd = new SqlCommand(sqlUpdClan, t.Connection, t))
                {
                    cmd.CommandTimeout = 5 * 60;
                    cmd.Parameters.AddWithValue("@clanTag", clan.ClanTag);
                    cmd.Parameters.AddWithValue("@clanId", clan.ClanId);
                    cmd.ExecuteNonQuery();
                }

                // This will be useful to propagate the change to the site latter...
                clan.OldTag = databaseClanTag;
            }

            const string sqlSetClanName =
                "update Main.Clan set Name = @name where (ClanId = @clanId);";
            using (var cmd = new SqlCommand(sqlSetClanName, t.Connection, t))
            {
                cmd.CommandTimeout = 5 * 60;
                cmd.Parameters.AddWithValue("@name", clan.Name);
                cmd.Parameters.AddWithValue("@clanId", clan.ClanId);
                cmd.ExecuteNonQuery();
            }

            using (var cmd = new SqlCommand("Main.SetClanDate", t.Connection, t))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 5 * 60;

                cmd.Parameters.AddWithValue("@ClanId", clan.ClanId);
                cmd.Parameters.AddWithValue("@Date", date);
                cmd.Parameters.AddWithValue("@MembershipMoment", clan.MembershipMoment);
                cmd.ExecuteNonQuery();
            }

            foreach (var player in clan.Players)
            {
                Set(player, t, !onlyMembership && player.Moment.Date == date);
            }

            // Apaga os jogadores para inserir novamente
            const string delHistSql = "delete from Main.ClanDatePlayer where (ClanId = @ClanId) and ([Date] = @Date);";
            using (var cmd = new SqlCommand(delHistSql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 5 * 60;

                cmd.Parameters.AddWithValue("@ClanId", clan.ClanId);
                cmd.Parameters.AddWithValue("@Date", date);
                cmd.ExecuteNonQuery();
            }

            const string delCurrentSql = "delete from [Current].ClanPlayer where (ClanId = @ClanId);";
            using (var cmd = new SqlCommand(delCurrentSql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 5 * 60;

                cmd.Parameters.AddWithValue("@ClanId", clan.ClanId);
                cmd.ExecuteNonQuery();
            }

            foreach (var player in clan.Players)
            {
                // Associa o jogador ao clã na data corrente
                const string sqlInsDatePlayer =
                    "insert into Main.ClanDatePlayer ([Date], PlayerId, RankId, ClanId) " +
                    "values (@date, @playerId, @rankId, @clanId);";
                using (var cmd = new SqlCommand(sqlInsDatePlayer, t.Connection, t))
                {
                    cmd.CommandTimeout = 5 * 60;
                    cmd.Parameters.AddWithValue("@clanId", clan.ClanId);
                    cmd.Parameters.AddWithValue("@date", date);
                    cmd.Parameters.AddWithValue("@playerId", player.Id);
                    cmd.Parameters.AddWithValue("@rankId", (int)player.Rank);
                    cmd.ExecuteNonQuery();
                }

                const string sqlInsPlayer =
                    "insert into [Current].ClanPlayer (ClanId, PlayerId, RankId) " +
                    "values (@ClanId, @PlayerId, @RankId);";
                using (var cmd = new SqlCommand(sqlInsPlayer, t.Connection, t))
                {
                    cmd.CommandTimeout = 5 * 60;
                    cmd.Parameters.AddWithValue("@ClanId", clan.ClanId);
                    cmd.Parameters.AddWithValue("@PlayerId", player.Id);
                    cmd.Parameters.AddWithValue("@RankId", (int)player.Rank);
                    cmd.ExecuteNonQuery();
                }
            }
        }


        /// <summary>
        /// Salva os valores esperados no BD, e dispara a atualização local
        /// </summary>
        public void Set(Wn8ExpectedValues ev, bool computeConsoleValues)
        {
            Log.DebugFormat("Salvando Wn8 Expected Values BD...");
            var sw = Stopwatch.StartNew();
            Execute(transaction => { Set(ev, computeConsoleValues, transaction); });
            Log.DebugFormat("Salva Wn8 Expected Values no BD em {0}.", sw.Elapsed);
        }

        private static void Set(Wn8ExpectedValues ev, bool computeConsoleValues, SqlTransaction t)
        {
            var now = DateTime.UtcNow;

            using (var cmd = new SqlCommand("Tanks.SetWn8ExpectedLoad", t.Connection, t))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 5 * 60;

                cmd.Parameters.AddWithValue("@date", now.Date);
                cmd.Parameters.AddWithValue("@source", ev.Source);
                cmd.Parameters.AddWithValue("@version", ev.Version);
                cmd.Parameters.AddWithValue("@moment", now);

                cmd.ExecuteNonQuery();
            }

            var setProcedure = ev.Source switch
            {
                Wn8ExpectedValuesSources.Xvm => "Tanks.[SetWn8PcExpectedValues]",
                Wn8ExpectedValuesSources.WotcStat => "Tanks.[SetWn8ConsoleExpectedValues]",
                _ => throw new ArgumentOutOfRangeException(nameof(ev.Source), ev.Source, @"Source not supported"),
            };

            using (var cmd = new SqlCommand(setProcedure, t.Connection, t))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 5 * 60;

                foreach (var v in ev.AllTanks)
                {
                    cmd.Parameters.Clear();

                    cmd.Parameters.AddWithValue("@date", now.Date);
                    cmd.Parameters.AddWithValue("@source", ev.Source);
                    cmd.Parameters.AddWithValue("@version", ev.Version);
                    cmd.Parameters.AddWithValue("@tankId", v.TankId);
                    cmd.Parameters.AddWithValue("@def", v.Def);
                    cmd.Parameters.AddWithValue("@frag", v.Frag);
                    cmd.Parameters.AddWithValue("@spot", v.Spot);
                    cmd.Parameters.AddWithValue("@damage", v.Damage);
                    cmd.Parameters.AddWithValue("@winRate", v.WinRate);

                    cmd.ExecuteNonQuery();
                }
            }

            if (computeConsoleValues)
            {
                var computeProcedure = ev.Source switch
                {
                    Wn8ExpectedValuesSources.Xvm => "Tanks.[CalculateWn8ConsoleExpectedFromXvm]",
                    Wn8ExpectedValuesSources.WotcStat => string.Empty, // no need to compute anything
                    _ => throw new ArgumentOutOfRangeException(nameof(ev.Source), ev.Source, @"Source not supported"),
                };

                if (!string.IsNullOrWhiteSpace(computeProcedure))
                {
                    using var cmd = new SqlCommand(computeProcedure, t.Connection, t);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandTimeout = 5 * 60;

                    cmd.ExecuteNonQuery();
                }
            }

        }

        public void Set(IEnumerable<TankPlayer> tankPlayers)
        {
            Log.DebugFormat("Salvando performance em tanques no BD...");
            var sw = Stopwatch.StartNew();
            Execute(transaction => { Set(tankPlayers.ToArray(), transaction); });
            Log.DebugFormat("Salva performance em tanques no BD em {0}.", sw.Elapsed);
        }

        private static void Set(IEnumerable<TankPlayer> tankPlayers, SqlTransaction t)
        {
            if (tankPlayers == null)
            {
                return;
            }

            var tps = tankPlayers.Where(o => o.LastBattleUnix > 0).ToArray();
            if (tps.Length < 1)
            {
                return;
            }

            if (tps.Select(tp => tp.PlayerId).Distinct().Count() != 1)
            {
                throw new ArgumentException(@"Only one player should be set at time.", nameof(tankPlayers));
            }

            bool firstTime;
            using (var cmd = new SqlCommand("Performance.HasAnyData", t.Connection, t))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 5 * 60;
                cmd.Parameters.AddWithValue("@playerId", tps[0].PlayerId);
                firstTime = (int)cmd.ExecuteScalar() == 0;
            }

            if (firstTime)
            {
                // Para que a contagem de mês funcione é preciso ancorar o que foi recém lido também no passado, de modo que a contagem inicie agora
                using var cmd = new SqlCommand("Performance.SetPlayerDate", t.Connection, t);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 5 * 60;

                foreach (var tp in tps)
                {
                    cmd.Parameters.Clear();

                    var anchorMoment = tp.LastBattle.AddYears(-1).AddMonths(-1);

                    cmd.Parameters.AddWithValue("@PlayerId", tp.PlayerId);
                    cmd.Parameters.AddWithValue("@Date", anchorMoment.Date);
                    cmd.Parameters.AddWithValue("@Moment", tp.Moment);
                    cmd.Parameters.AddWithValue("@TankId", tp.TankId);
                    cmd.Parameters.AddWithValue("@LastBattle", anchorMoment);
                    cmd.Parameters.AddWithValue("@TreesCut", tp.TreesCut);
                    cmd.Parameters.AddWithValue("@MaxFrags", tp.MaxFrags);
                    cmd.Parameters.AddWithValue("@MarkOfMastery", tp.MarkOfMastery);
                    cmd.Parameters.AddWithValue("@BattleLifeTimeSeconds", tp.BattleLifeTimeSeconds);
                    cmd.Parameters.AddWithValue("@Spotted", tp.All.Spotted);
                    cmd.Parameters.AddWithValue("@DamageAssistedTrack", tp.All.DamageAssistedTrack);
                    cmd.Parameters.AddWithValue("@DamageAssistedRadio", tp.All.DamageAssistedRadio);
                    cmd.Parameters.AddWithValue("@CapturePoints", tp.All.CapturePoints);
                    cmd.Parameters.AddWithValue("@DroppedCapturePoints", tp.All.DroppedCapturePoints);
                    cmd.Parameters.AddWithValue("@Battles", tp.All.Battles);
                    cmd.Parameters.AddWithValue("@Wins", tp.All.Wins);
                    cmd.Parameters.AddWithValue("@Losses", tp.All.Losses);
                    cmd.Parameters.AddWithValue("@Kills", tp.All.Kills);
                    cmd.Parameters.AddWithValue("@SurvivedBattles", tp.All.SurvivedBattles);
                    cmd.Parameters.AddWithValue("@DamageDealt", tp.All.DamageDealt);
                    cmd.Parameters.AddWithValue("@DamageReceived", tp.All.DamageReceived);
                    cmd.Parameters.AddWithValue("@PiercingsReceived", tp.All.PiercingsReceived);
                    cmd.Parameters.AddWithValue("@Hits", tp.All.Hits);
                    cmd.Parameters.AddWithValue("@NoDamageDirectHitsReceived", tp.All.NoDamageDirectHitsReceived);
                    cmd.Parameters.AddWithValue("@ExplosionHits", tp.All.ExplosionHits);
                    cmd.Parameters.AddWithValue("@Piercings", tp.All.Piercings);
                    cmd.Parameters.AddWithValue("@Shots", tp.All.Shots);
                    cmd.Parameters.AddWithValue("@ExplosionHitsReceived", tp.All.ExplosionHitsReceived);
                    cmd.Parameters.AddWithValue("@XP", tp.All.XP);
                    cmd.Parameters.AddWithValue("@DirectHitsReceived", tp.All.DirectHitsReceived);
                    cmd.Parameters.AddWithValue("@IsAnchor", true);

                    cmd.ExecuteNonQuery();
                }
            }

            using (var cmd = new SqlCommand("Performance.SetPlayerDate", t.Connection, t))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 5 * 60;

                foreach (var tp in tps)
                {
                    cmd.Parameters.Clear();

                    cmd.Parameters.AddWithValue("@PlayerId", tp.PlayerId);
                    cmd.Parameters.AddWithValue("@Date", tp.Date);
                    cmd.Parameters.AddWithValue("@Moment", tp.Moment);
                    cmd.Parameters.AddWithValue("@TankId", tp.TankId);
                    cmd.Parameters.AddWithValue("@LastBattle", tp.LastBattle);
                    cmd.Parameters.AddWithValue("@TreesCut", tp.TreesCut);
                    cmd.Parameters.AddWithValue("@MaxFrags", tp.MaxFrags);
                    cmd.Parameters.AddWithValue("@MarkOfMastery", tp.MarkOfMastery);
                    cmd.Parameters.AddWithValue("@BattleLifeTimeSeconds", tp.BattleLifeTimeSeconds);
                    cmd.Parameters.AddWithValue("@Spotted", tp.All.Spotted);
                    cmd.Parameters.AddWithValue("@DamageAssistedTrack", tp.All.DamageAssistedTrack);
                    cmd.Parameters.AddWithValue("@DamageAssistedRadio", tp.All.DamageAssistedRadio);
                    cmd.Parameters.AddWithValue("@CapturePoints", tp.All.CapturePoints);
                    cmd.Parameters.AddWithValue("@DroppedCapturePoints", tp.All.DroppedCapturePoints);
                    cmd.Parameters.AddWithValue("@Battles", tp.All.Battles);
                    cmd.Parameters.AddWithValue("@Wins", tp.All.Wins);
                    cmd.Parameters.AddWithValue("@Losses", tp.All.Losses);
                    cmd.Parameters.AddWithValue("@Kills", tp.All.Kills);
                    cmd.Parameters.AddWithValue("@SurvivedBattles", tp.All.SurvivedBattles);
                    cmd.Parameters.AddWithValue("@DamageDealt", tp.All.DamageDealt);
                    cmd.Parameters.AddWithValue("@DamageReceived", tp.All.DamageReceived);
                    cmd.Parameters.AddWithValue("@PiercingsReceived", tp.All.PiercingsReceived);
                    cmd.Parameters.AddWithValue("@Hits", tp.All.Hits);
                    cmd.Parameters.AddWithValue("@NoDamageDirectHitsReceived", tp.All.NoDamageDirectHitsReceived);
                    cmd.Parameters.AddWithValue("@ExplosionHits", tp.All.ExplosionHits);
                    cmd.Parameters.AddWithValue("@Piercings", tp.All.Piercings);
                    cmd.Parameters.AddWithValue("@Shots", tp.All.Shots);
                    cmd.Parameters.AddWithValue("@ExplosionHitsReceived", tp.All.ExplosionHitsReceived);
                    cmd.Parameters.AddWithValue("@XP", tp.All.XP);
                    cmd.Parameters.AddWithValue("@DirectHitsReceived", tp.All.DirectHitsReceived);
                    cmd.Parameters.AddWithValue("@IsAnchor", false);

                    cmd.ExecuteNonQuery();
                }
            }

            // Get the current medals/ribbons so only a diff get upload
            var oldMedals = GetMedalsForPlayer(tps[0].PlayerId, t).ToDictionary(m => (m.tankId, m.medalCode));

            if (oldMedals.Count <= 0)
            {
                // Easier to do a bulk copy
                SetMedalsByBulkCopy(t, tps);
                return;
            }

            var toUpdate = new List<(long tankId, string medalCode, int count, long battles)>();

            foreach (var tank in tps)
            {
                if ((tank?.All?.Achievements != null) && (tank.All.Achievements.Count > 0))
                {
                    foreach (var medal in tank.All.Achievements)
                    {
                        if (oldMedals.TryGetValue((tank.TankId, medal.Key), out var previous))
                        {
                            if (previous.battles != tank.All.Battles || previous.count != medal.Value)
                            {
                                toUpdate.Add((tank.TankId, medal.Key, medal.Value, tank.All.Battles));
                            }
                        }
                        else
                        {
                            toUpdate.Add((tank.TankId, medal.Key, medal.Value, tank.All.Battles));
                        }
                    }
                }

                if ((tank?.All?.Ribbons != null) && (tank.All.Ribbons.Count > 0))
                {
                    foreach (var medal in tank.All.Ribbons)
                    {
                        if (oldMedals.TryGetValue((tank.TankId, medal.Key), out var previous))
                        {
                            if (previous.battles != tank.All.Battles || previous.count != medal.Value)
                            {
                                toUpdate.Add((tank.TankId, medal.Key, medal.Value, tank.All.Battles));
                            }
                        }
                        else
                        {
                            toUpdate.Add((tank.TankId, medal.Key, medal.Value, tank.All.Battles));
                        }
                    }
                }
            }

            if (toUpdate.Count <= 0)
            {
                return;
            }

            using (var cmd = new SqlCommand("Achievements.SetPlayerMedal", t.Connection, t))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 5 * 60;

                foreach (var (tankId, medalCode, count, battles) in toUpdate)
                {
                    cmd.Parameters.Clear();

                    cmd.Parameters.AddWithValue("@playerId", tps[0].PlayerId);
                    cmd.Parameters.AddWithValue("@tankId", tankId);
                    cmd.Parameters.AddWithValue("@medalCode", medalCode);
                    cmd.Parameters.AddWithValue("@count", count);
                    cmd.Parameters.AddWithValue("@battles", battles);

                    try
                    {
                        cmd.ExecuteNonQuery();
                    }
                    catch (SqlException ex)
                    {
                        Log.Error($"Invoking Achievements.SetPlayerMedal({tps[0].PlayerId}, {tankId}, {medalCode}, {count}, {battles})", ex);
                    }
                    
                }
            }

        }

        private static IEnumerable<(long tankId, string medalCode, int count, long battles)> GetMedalsForPlayer(long playerId, SqlTransaction t)
        {
            var res = new List<(long tankId, string medalCode, int count, long battles)>();

            const string query = "select TankId, MedalCode, [Count], Battles from Achievements.PlayerMedal where PlayerId = @playerId;";
            using var cmd = new SqlCommand(query, t.Connection, t);
            cmd.CommandType = CommandType.Text;
            cmd.CommandTimeout = 15;
            cmd.Parameters.AddWithValue("@playerId", playerId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                res.Add((reader.GetNonNullValue<long>(0), reader.GetNonNullValue<string>(1),
                    reader.GetNonNullValue<int>(2), reader.GetNonNullValue<long>(3)));
            }

            return res;
        }

        private static void SetMedalsByBulkCopy(SqlTransaction t, TankPlayer[] tankPlayer)
        {
            var medals = CreateMedalsTable(tankPlayer);
            if (medals.Rows.Count > 0)
            {
                const string delSql = "delete Achievements.PlayerMedal where (PlayerId = @playerId);";
                using (var cmd = new SqlCommand(delSql, t.Connection, t))
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandTimeout = 5 * 60;
                    cmd.Parameters.AddWithValue("@playerId", tankPlayer[0].PlayerId);
                    cmd.ExecuteNonQuery();
                }

                using var bc = new SqlBulkCopy(t.Connection, SqlBulkCopyOptions.Default, t);
                bc.DestinationTableName = "Achievements.PlayerMedal";
                bc.WriteToServer(medals);
            }
        }


        private static DataTable CreateMedalsTable(IEnumerable<TankPlayer> tanks)
        {
            var t = new DataTable("TankMedal");

            var dc2 = new DataColumn("PlayerId", typeof(long));
            t.Columns.Add(dc2);

            var dc3 = new DataColumn("TankId", typeof(long));
            t.Columns.Add(dc3);

            var dc4 = new DataColumn("MedalCode", typeof(string));
            t.Columns.Add(dc4);

            var dc5 = new DataColumn("Count", typeof(int));
            t.Columns.Add(dc5);

            var dc6 = new DataColumn("Battles", typeof(long));
            t.Columns.Add(dc6);

            foreach (var tank in tanks)
            {
                if ((tank?.All?.Achievements != null) && (tank.All.Achievements.Count > 0))
                {
                    foreach (var medal in tank.All.Achievements)
                    {
                        t.Rows.Add(tank.PlayerId, tank.TankId, medal.Key, medal.Value, tank.All.Battles);
                    }
                }

                if ((tank?.All?.Ribbons != null) && (tank.All.Ribbons.Count > 0))
                {
                    foreach (var medal in tank.All.Ribbons)
                    {
                        t.Rows.Add(tank.PlayerId, tank.TankId, medal.Key, medal.Value, tank.All.Battles);
                    }
                }
            }

            return t;
        }

        public void AssociateDiscordUserToPlayer(long userId, long playerId)
        {
            Execute(t => AssociateDiscordUserToPlayer(userId, playerId, t));
        }

        private static void AssociateDiscordUserToPlayer(long userId, long playerId, SqlTransaction t)
        {
            using var cmd = new SqlCommand("Discord.AssociateUserToPlayer", t.Connection, t);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@userId", userId);
            cmd.Parameters.AddWithValue("@playerId", playerId);
            cmd.ExecuteNonQuery();
        }

        public void SetClanFlag(long clanId, string flagCode)
        {
            Execute(transaction => { SetClanFlag(clanId, flagCode, transaction); });
        }

        private static void SetClanFlag(long clanId, string flagCode, SqlTransaction t)
        {
            const string sql =
                "update Main.Clan set FlagCode = @flagCode where (ClanId = @clanId);";
            using var cmd = new SqlCommand(sql, t.Connection, t);
            cmd.CommandType = CommandType.Text;
            cmd.CommandTimeout = 5 * 60;

            cmd.Parameters.AddWithValue("@clanId", clanId);
            if (string.IsNullOrWhiteSpace(flagCode))
            {
                cmd.Parameters.AddWithValue("@flagCode", DBNull.Value);
            }
            else
            {
                cmd.Parameters.AddWithValue("@flagCode", flagCode);
            }

            cmd.ExecuteNonQuery();
        }

        public void Set(Platform platform, IEnumerable<Tank> tanks)
        {
            Log.Debug($"Saving {platform} tanks on DB...");
            var sw = Stopwatch.StartNew();
            Execute(transaction => { Set(platform, tanks.ToArray(), transaction); });
            Log.Debug($"Saved {platform} tanks in {sw.Elapsed}.");
        }

        private static void Set(Platform platform, IEnumerable<Tank> tanks, SqlTransaction t)
        {
            var procedure = platform switch
            {
                Platform.PC => "Tanks.SetPcTank",
                Platform.Console => "Tanks.SetTank",
                _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null)
            };


            using var cmd = new SqlCommand(procedure, t.Connection, t) { CommandType = CommandType.StoredProcedure, CommandTimeout = 5 * 60 };
            foreach (var tank in tanks)
            {

                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@tankId", tank.TankId);
                cmd.Parameters.AddWithValue("@name", tank.Name);
                cmd.Parameters.AddWithValue("@shortName", tank.ShortName);
                cmd.Parameters.AddWithValue("@nationId", (int)tank.Nation);
                cmd.Parameters.AddWithValue("@tier", tank.Tier);
                cmd.Parameters.AddWithValue("@typeId", (int)tank.Type);
                cmd.Parameters.AddWithValue("@tag", tank.Tag);
                cmd.Parameters.AddWithValue("@isPremium", tank.IsPremium);

                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        ///     Retorna a gamer tag de um jogador, ou null caso ele não exista
        /// </summary>
        private static string GetGamerTag(long playerId, SqlTransaction t)
        {
            const string sql = "select GamerTag from Main.Player where PlayerId = @playerId;";
            using var cmd = new SqlCommand(sql, t.Connection, t);
            cmd.CommandTimeout = 5 * 60;
            cmd.Parameters.AddWithValue("@playerId", playerId);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return reader.GetNonNullValue<string>(0);
            }

            return null;
        }

        /// <summary>
        ///     Retorna o nome do clã, ou nulo caso ele não exista
        /// </summary>
        private static string GetClanTag(long clanId, SqlTransaction t)
        {
            const string sql =
                "select ClanTag from Main.Clan where (ClanId = @clanId);";
            using var cmd = new SqlCommand(sql, t.Connection, t);
            cmd.CommandTimeout = 5 * 60;
            cmd.Parameters.AddWithValue("@clanId", clanId);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return reader.GetNonNullValue<string>(0);
            }

            return null;
        }

        /// <summary>
        ///     Calcula os valores das marcas de excelência
        /// </summary>
        public void CalculateMoE(int utcShiftToCalculate = 0)
        {
            Log.Debug("Calculando MoE...");
            var sw = Stopwatch.StartNew();
            Execute(t => CalculateMoE(utcShiftToCalculate, t));
            Log.DebugFormat("Calculo do MoE em {0}.", sw.Elapsed);
        }

        /// <summary>
        ///     Calcula os valores de referência dos tanques
        /// </summary>
        public void CalculateReference(int utcShiftToCalculate = -7, int maxDates = 2)
        {
            Log.Debug("Calculando Dados de Tanques...");
            var sw = Stopwatch.StartNew();
            Execute(t => CalculateReference(utcShiftToCalculate, maxDates, t));
            Log.DebugFormat("Calculo de Dados de Tanques em {0}.", sw.Elapsed);
        }

        private static void CalculateMoE(int utcShiftToCalculate, SqlTransaction t)
        {
            using var cmd = new SqlCommand("Performance.CalculateMoEPercentile", t.Connection, t)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = 4 * 60 * 60 // BRUTALMENTE LENTO!
            };

            cmd.Parameters.AddWithValue("@utcShift", utcShiftToCalculate);
            cmd.ExecuteNonQuery();
        }

        private static void CalculateReference(int utcShiftToCalculate, int maxDates, SqlTransaction t)
        {
            using var cmd = new SqlCommand("Performance.CalculateReferenceValues", t.Connection, t)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = 4 * 60 * 60 // BRUTALMENTE LENTO!
            };
            // Muitos minutos: pode ser bastante lento!
            cmd.Parameters.AddWithValue("@utcShift", utcShiftToCalculate);
            cmd.Parameters.AddWithValue("@maxDates", maxDates);
            cmd.ExecuteNonQuery();
        }

        public void BalanceClanSchedule(int minPlayers, int maxPlayers)
        {
            Log.DebugFormat("Balanceando Schedule entre {0} e {1}...", minPlayers, maxPlayers);
            var sw = Stopwatch.StartNew();
            Execute(transaction => { BalanceClanSchedule(minPlayers, maxPlayers, transaction); });
            Log.DebugFormat("Balanceado Schedule entre {0} e {1} em {2}.", minPlayers, maxPlayers, sw.Elapsed);
        }

        private static void BalanceClanSchedule(int minPlayers, int maxPlayers, SqlTransaction t)
        {
            using var cmd = new SqlCommand("Main.BalanceSchedule", t.Connection, t);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandTimeout = 5 * 60;
            cmd.Parameters.AddWithValue("@minPlayers", minPlayers);
            cmd.Parameters.AddWithValue("@maxPlayers", maxPlayers);
            cmd.ExecuteNonQuery();
        }

        public void Add(Player player)
        {
            Log.DebugFormat("Salvando player {0}.{1} no BD...", player.Name, player.Id);
            var sw = Stopwatch.StartNew();
            Execute(transaction => { Set(player, transaction, false); });
            Log.DebugFormat("Salvo player {0}.{1} no BD em {2}.", player.Name, player.Id, sw.Elapsed);
        }

        public void Set(Player player)
        {
            Log.DebugFormat("Salvando player {0}.{1} no BD...", player.Name, player.Id);
            var sw = Stopwatch.StartNew();
            Execute(transaction => { Set(player, transaction, true); });
            Log.DebugFormat("Salvo player {0}.{1} no BD em {2}.", player.Name, player.Id, sw.Elapsed);
        }

        [SuppressMessage("ReSharper", "StringLiteralTypo", Justification = "The database has platform misspelled.")]
        private static void Set(Player player, SqlTransaction t, bool updateStatistics)
        {
            var databaseGamerTag = GetGamerTag(player.Id, t);

            // hack: Contorno enquanto a WG não corrige a API deles
            if (string.IsNullOrWhiteSpace(player.Name) ||
                player.Name.StartsWith(":id:", StringComparison.OrdinalIgnoreCase))
            {
                player.Name = string.IsNullOrWhiteSpace(databaseGamerTag) ? $"id{player.Id}di" : databaseGamerTag;
            }

            if (player.Name.Length > 25)
            {
                Log.WarnFormat("Jogador {0}.{1}@{2} terá o nome truncado!", player.Id, player.Name, player.Platform);
                player.Name = player.Name.Substring(0, 25);
            }

            if (databaseGamerTag == null)
            {
                // Caso um jogador não exista no sistema, crie
                const string sqlInsPlayer =
                    "insert into Main.Player (PlayerId, GamerTag, PlataformId) values (@playerId, @gamerTag, @plataformId);";
                using var cmd = new SqlCommand(sqlInsPlayer, t.Connection, t);
                cmd.CommandTimeout = 5 * 60;
                cmd.Parameters.AddWithValue("@playerId", player.Id);
                cmd.Parameters.AddWithValue("@gamerTag", player.Name);
                cmd.Parameters.AddWithValue("@plataformId", (int)player.Platform);
                cmd.ExecuteNonQuery();
            }
            else if (databaseGamerTag != player.Name)
            {
                // Atualizou o gamer tag...
                const string sqlUpdPlayer = "update Main.Player set GamerTag = @gamerTag where PlayerId = @playerId;";
                using (var cmd = new SqlCommand(sqlUpdPlayer, t.Connection, t))
                {
                    cmd.CommandTimeout = 5 * 60;
                    cmd.Parameters.AddWithValue("@gamerTag", player.Name);
                    cmd.Parameters.AddWithValue("@playerId", player.Id);
                    cmd.ExecuteNonQuery();
                }

                const string sqlUpdPlayerHist = "insert into Main.PlayerGamerTagChange (PlayerId, Moment, PreviousGamerTag, CurrentGamerTag) values (@PlayerId, @Moment, @PreviousGamerTag, @CurrentGamerTag)";
                using (var cmd = new SqlCommand(sqlUpdPlayerHist, t.Connection, t))
                {
                    cmd.CommandTimeout = 5 * 60;
                    cmd.Parameters.AddWithValue("@PlayerId", player.Id);
                    cmd.Parameters.AddWithValue("@Moment", DateTime.UtcNow);
                    cmd.Parameters.AddWithValue("@PreviousGamerTag", databaseGamerTag);
                    cmd.Parameters.AddWithValue("@CurrentGamerTag", player.Name);
                    cmd.ExecuteNonQuery();
                }
            }

            if (!updateStatistics)
            {
                return;
            }

            using (var cmd = new SqlCommand("Main.SetPlayerDate", t.Connection, t))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 5 * 60;

                cmd.Parameters.AddWithValue("@PlayerId", player.Id);
                cmd.Parameters.AddWithValue("@Date", player.Moment.Date);
                cmd.Parameters.AddWithValue("@Moment", player.Moment);
                cmd.Parameters.AddWithValue("@TotalBattles", player.TotalBattles);
                cmd.Parameters.AddWithValue("@MonthBattles", player.MonthBattles);
                cmd.Parameters.AddWithValue("@TotalWinRate", player.TotalWinRate.Normalize());
                cmd.Parameters.AddWithValue("@MonthWinRate", player.MonthWinRate.Normalize());
                cmd.Parameters.AddWithValue("@TotalWn8", player.TotalWn8.Normalize());
                cmd.Parameters.AddWithValue("@MonthWn8", player.MonthWn8.Normalize());
                cmd.Parameters.AddWithValue("@IsPatched", player.IsPatched);
                cmd.Parameters.AddWithValue("@Origin", player.Origin);
                cmd.Parameters.AddWithValue("@TotalTier", player.TotalTier);
                cmd.Parameters.AddWithValue("@MonthTier", player.MonthTier);

                cmd.Parameters.AddWithValue("@Tier10TotalBattles", player.Tier10TotalBattles);
                cmd.Parameters.AddWithValue("@Tier10TotalWinRate", player.Tier10TotalWinRate);
                cmd.Parameters.AddWithValue("@Tier10TotalWn8", player.Tier10TotalWn8);

                cmd.Parameters.AddWithValue("@Tier10MonthBattles", player.Tier10MonthBattles);
                cmd.Parameters.AddWithValue("@Tier10MonthWinRate", player.Tier10MonthWinRate);
                cmd.Parameters.AddWithValue("@Tier10MonthWn8", player.Tier10MonthWn8);

                cmd.ExecuteNonQuery();
            }
        }

        public void SetClanCalculation(Clan clan)
        {
            Log.DebugFormat("Salvando calculos do clã {0} no BD em {1:yyyy-MM-dd}...", clan.ClanTag, clan.Date);
            var sw = Stopwatch.StartNew();
            Execute(transaction => { SetClanCalculation(clan, transaction); });
            Log.DebugFormat("Salvos calculos do clã {0} no BD em {1:yyyy-MM-dd} em {2}.", clan.ClanTag, clan.Date, sw.Elapsed);
        }

        private static void SetClanCalculation(Clan clan, SqlTransaction t)
        {
            if (clan.ClanId == 0)
            {
                throw new ArgumentException(@"The ClanId member can't be 0!", nameof(clan.ClanId));
            }

            using var cmd = new SqlCommand("Main.SetClanCalculation", t.Connection, t);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandTimeout = 5 * 60;

            cmd.Parameters.AddWithValue("@ClanId", clan.ClanId);
            cmd.Parameters.AddWithValue("@Date", clan.MembershipMoment.Date);

            cmd.Parameters.AddWithValue("@CalculationMoment", DateTime.UtcNow);

            cmd.Parameters.AddWithValue("@TotalMembers", clan.Count);
            cmd.Parameters.AddWithValue("@ActiveMembers", clan.Active);

            cmd.Parameters.AddWithValue("@TotalBattles", clan.TotalBattles);
            cmd.Parameters.AddWithValue("@MonthBattles", clan.ActiveBattles);

            cmd.Parameters.AddWithValue("@TotalWinRate", clan.TotalWinRate.Normalize());
            cmd.Parameters.AddWithValue("@MonthWinrate", clan.ActiveWinRate.Normalize());

            cmd.Parameters.AddWithValue("@TotalWN8", clan.TotalWn8.Normalize());
            cmd.Parameters.AddWithValue("@WN8a", clan.ActiveWn8.Normalize());
            cmd.Parameters.AddWithValue("@WN8t15", clan.Top15Wn8.Normalize());
            cmd.Parameters.AddWithValue("@WN8t7", clan.Top7Wn8.Normalize());

            cmd.Parameters.AddWithValue("@Top15AvgTier", clan.Top15AvgTier.Normalize());
            cmd.Parameters.AddWithValue("@ActiveAvgTier", clan.ActiveAvgTier.Normalize());
            cmd.Parameters.AddWithValue("@TotalAvgTier", clan.TotalAvgTier.Normalize());

            cmd.ExecuteNonQuery();
        }

        /// <summary>
        ///     Adds the specified clan.
        /// </summary>
        public void Add(Clan clan)
        {
            Log.DebugFormat("Adicionando clã {2}.{0} no BD em {1:yyyy-MM-dd}...", clan.ClanTag, clan.Date, clan.ClanId);
            var sw = Stopwatch.StartNew();
            Execute(t => Add(clan, t));
            Log.DebugFormat("Adicionado clã {3}.{0} no BD em {1:yyyy-MM-dd} em {2}.", clan.ClanTag, clan.Date, sw.Elapsed, clan.ClanId);
        }

        private static void Add(Clan clan, SqlTransaction t)
        {
            using var cmd = new SqlCommand("Main.AddClan", t.Connection, t);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandTimeout = 5 * 60;
            cmd.Parameters.AddWithValue("@clanId", clan.ClanId);
            cmd.Parameters.AddWithValue("@clanTag", clan.ClanTag);
            cmd.Parameters.AddWithValue("@country",
                string.IsNullOrWhiteSpace(clan.Country) ? (object)DBNull.Value : clan.Country.ToLowerInvariant());

            cmd.ExecuteNonQuery();
        }

        public void EnableClan(long clanId)
        {
            Log.DebugFormat("Enabling clan {0}...", clanId);
            var sw = Stopwatch.StartNew();
            Execute(t => EnableDisableClan(clanId, true, DisabledReason.NotDisabled, t));
            Log.DebugFormat("Enabled clan {0} in {1}.", clanId, sw.Elapsed);
        }

        public void DisableClan(long clanId, DisabledReason disabledReason)
        {
            Log.DebugFormat("Disabing clan {0} for reason {1}...", clanId, disabledReason);
            var sw = Stopwatch.StartNew();
            Execute(t => EnableDisableClan(clanId, false, disabledReason, t));
            Log.DebugFormat("Disabled clan {0} in {1}.", clanId, sw.Elapsed);
        }

        private static void EnableDisableClan(long clanId, bool enable,
            DisabledReason disabledReason, SqlTransaction t)
        {
            const string sql =
                "update Main.Clan set [Enabled] = @enableDisable, DisabledReason = @disabledReason where (ClanId = @clanId);";
            using var cmd = new SqlCommand(sql, t.Connection, t);
            cmd.CommandType = CommandType.Text;
            cmd.CommandTimeout = 5 * 60;

            cmd.Parameters.AddWithValue("@clanId", clanId);
            cmd.Parameters.AddWithValue("@enableDisable", enable);
            cmd.Parameters.AddWithValue("@disabledReason", (int)disabledReason);

            cmd.ExecuteNonQuery();
        }

        public void ReAddClans(int maxToAutoAdd)
        {
            Log.Debug("Adicionando clãs novamente...");
            var sw = Stopwatch.StartNew();
            Execute(t => ReAddClans(maxToAutoAdd, t));
            Log.DebugFormat("Readicionados clãs em {0}.", sw.Elapsed);
        }

        private static void ReAddClans(int maxToAutoAdd, SqlTransaction t)
        {
            const string sql = "Main.ReAddClan";
            using var cmd = new SqlCommand(sql, t.Connection, t);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandTimeout = 5 * 60;
            cmd.Parameters.AddWithValue("@maxToAutoAdd", maxToAutoAdd);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Purges a player from the database
        /// </summary>
        /// <remarks>
        /// To more easily complain with Data Deletions laws.
        /// </remarks>
        public void PurgePlayer(long playerId)
        {
            Log.Debug($"Purging player {playerId}...");
            var sw = Stopwatch.StartNew();
            Execute(t => PurgePlayer(playerId, t));
            Log.DebugFormat("Player purged in {0}.", sw.Elapsed);
        }

        private static void PurgePlayer(long playerId, SqlTransaction t)
        {
            const string sql = "Support.PurgePlayer";
            using var cmd = new SqlCommand(sql, t.Connection, t);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandTimeout = 5 * 60;
            cmd.Parameters.AddWithValue("@id", playerId);
            cmd.ExecuteNonQuery();
        }

        public void Set(IEnumerable<Medal> medals)
        {
            Log.Debug("Saving medals...");
            var sw = Stopwatch.StartNew();
            Execute(t => Set(medals, t));
            Log.DebugFormat("Saved medals in {0}.", sw.Elapsed);
        }

        private static void Set(IEnumerable<Medal> medals, SqlTransaction t)
        {
            const string sql = "Achievements.SetMedal";
            using var cmd = new SqlCommand(sql, t.Connection, t);
            cmd.CommandType = CommandType.StoredProcedure;

            foreach (var medal in medals)
            {
                if (string.IsNullOrWhiteSpace(medal.Name))
                {
                    Log.Warn($"Medal {medal.Code} don't have a name!");
                    medal.Name = medal.Code;
                    continue;
                }

                cmd.Parameters.Clear();

                cmd.Parameters.AddWithValue("@MedalCode", medal.Code);
                cmd.Parameters.AddWithValue("@Name", medal.Name);
                cmd.Parameters.AddWithValue("@Description", (object)medal.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@HeroInformation", (object)medal.HeroInformation ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Condition", (object)medal.Condition ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CategoryId", (int)medal.Category);
                cmd.Parameters.AddWithValue("@TypeId", (int)medal.Type);
                cmd.Parameters.AddWithValue("@SectionId", (int)medal.Section);

                cmd.ExecuteNonQuery();
            }
        }


        /// <summary>
        /// Saves a load of calculated MoEs
        /// </summary>
        public void Set(MoeMethod method, DateTime moment, IEnumerable<TankMoe> moes)
        {
            switch (method)
            {
                case MoeMethod.WoTConsoleRu:
                    SetWoTConsoleRuMoe(moment, moes.ToArray());
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(method), method, @"MoE Method not supported!");
            }
        }

        private void SetWoTConsoleRuMoe(DateTime moment, TankMoe[] moes)
        {
            Log.Debug("Saving WoTConsole.ru MoEs...");
            var sw = Stopwatch.StartNew();
            Execute(t => SetWoTConsoleRuMoe(moment, moes, t));
            Log.DebugFormat("Saved WoTConsole.ru MoEs in {0}.", sw.Elapsed);
        }

        private void SetWoTConsoleRuMoe(DateTime moment, TankMoe[] moes, SqlTransaction t)
        {
            var date = moment.Date.RemoveKind();
            var count = moes.Length;

            const string sql = "Performance.SetMoEWoTConsoleRuLoad";
            using (var cmd = new SqlCommand(sql, t.Connection, t))
            {
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@date", date);
                cmd.Parameters.AddWithValue("@moment", moment);
                cmd.Parameters.AddWithValue("@count", count);

                cmd.ExecuteNonQuery();
            }

            const string delSql = "delete from [Performance].[MoEWoTConsoleRu] where [Date] = @date;";
            using (var cmd = new SqlCommand(delSql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.AddWithValue("@date", date);
                cmd.ExecuteNonQuery();
            }

            const string insSql = "insert into [Performance].[MoEWoTConsoleRu] ([Date], TankId, Battles, MaxAvgDmg) values (@date, @tankId, @battles, @maxAvgDmg);";
            using (var cmd = new SqlCommand(insSql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;

                foreach (var m in moes)
                {
                    cmd.Parameters.Clear();

                    cmd.Parameters.AddWithValue("@date", date);
                    cmd.Parameters.AddWithValue("@tankId", m.TankId);
                    cmd.Parameters.AddWithValue("@battles", m.NumberOfBattles);
                    cmd.Parameters.AddWithValue("@maxAvgDmg", m.HighMarkDamage);

                    cmd.ExecuteNonQuery();
                }
            }

        }

        public void PurgeOldPlayers(int months)
        {
            Log.Debug("PurgeOldPlayers...");
            var sw = Stopwatch.StartNew();
            Execute(t => PurgeOldPlayers(months, t));
            Log.DebugFormat("PurgeOldPlayers in {0}.", sw.Elapsed);
        }

        private static void PurgeOldPlayers(int months, SqlTransaction t)
        {
            const string sql = "Support.PurgeOldPlayers";
            using var cmd = new SqlCommand(sql, t.Connection, t);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandTimeout = 60 * 60; // 1h... pode demorar

            cmd.Parameters.AddWithValue("@onlyReport", false);
            cmd.Parameters.AddWithValue("@lunarMonths", months);

            cmd.ExecuteNonQuery();
        }
    }
}