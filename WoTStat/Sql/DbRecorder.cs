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
            Log.DebugFormat("Setando clã {0}@{1} no BD...", clan.ClanTag, clan.Plataform);
            var sw = Stopwatch.StartNew();
            Execute(transaction => { Set(clan, onlyMembership, transaction); });
            Log.DebugFormat("Salvo no BD clã {0}@{1} em {2}.", clan.ClanTag, clan.Plataform, sw.Elapsed);
        }

        private static void Set(Clan clan, bool onlyMembership, SqlTransaction t)
        {
            var date = clan.Date;

            var databaseClanTag = GetClanTag(clan.Plataform, clan.ClanId, t);
            if (string.IsNullOrWhiteSpace(databaseClanTag))
            {
                throw new ApplicationException(
                    $"Já deveria existir em Main.Clan o clã {clan.ClanId}.{clan.ClanTag}@{clan.Plataform}!");
            }

            if (databaseClanTag != clan.ClanTag)
            {
                // verifica se o novo nome não vai conflitar com algo que ainda não tenha sido refletido.
                long? otherClanId = null;
                const string sqlCheckName =
                    "select ClanId from Main.Clan where (ClanTag = @clanTag) and (PlataformId = @plataformId);";
                using (var cmd = new SqlCommand(sqlCheckName, t.Connection, t))
                {
                    cmd.CommandTimeout = 5 * 60;
                    cmd.Parameters.AddWithValue("@clanTag", clan.ClanTag);
                    cmd.Parameters.AddWithValue("@plataformId", (int) clan.Plataform);
                    var o = cmd.ExecuteScalar();
                    if (o != null)
                    {
                        var existingId = (long) o;
                        if (existingId != clan.ClanId)
                        {
                            // O novo nome colide com algum outro...
                            otherClanId = existingId;
                            Log.WarnFormat("O novo tag de {0}.{1}.{2} colide com {0}.{1}.{3}...",
                                clan.ClanTag, clan.Plataform, clan.ClanId, existingId);
                        }
                    }
                }

                if (otherClanId.HasValue)
                {
                    // O novo nome colide com algum outro no BD. É um clã ativo?
                    bool isEnabled;
                    const string sqlIsEnabled =
                        "select [Enabled] from Main.Clan where (ClanId = @clanId) and (PlataformId = @plataformId);";
                    using (var cmd = new SqlCommand(sqlIsEnabled, t.Connection, t))
                    {
                        cmd.CommandTimeout = 5 * 60;
                        cmd.Parameters.AddWithValue("@clanId", otherClanId.Value);
                        cmd.Parameters.AddWithValue("@plataformId", (int) clan.Plataform);
                        isEnabled = (bool) cmd.ExecuteScalar();
                    }
                    if (isEnabled)
                    {
                        // Basta esperar que o anterior seja atualizado para seja lá o que for
                        Log.Warn("O clã ainda está ativo, basta esperar que seja atualizado para evitar o conflito");
                        return;
                    }

                    // Troca o tag do antigo para algo que não dê conflito
                    const string sqlChangeDisabled =
                        "update Main.Clan set ClanTag = @clanTag where (ClanId = @clanId) and (PlataformId = @plataformId);";
                    using (var cmd = new SqlCommand(sqlChangeDisabled, t.Connection, t))
                    {
                        cmd.CommandTimeout = 5 * 60;

                        var rand = new Random();
                        var disabledName = "+" + rand.Next(0, 10000).ToString(CultureInfo.InvariantCulture);

                        Log.WarnFormat("Trocando o tag do clã inativo {0}.{1}.{2} para {3}",
                            clan.ClanTag, clan.Plataform, otherClanId.Value, disabledName);

                        cmd.Parameters.AddWithValue("@clanTag", disabledName);
                        cmd.Parameters.AddWithValue("@clanId", otherClanId.Value);
                        cmd.Parameters.AddWithValue("@plataformId", (int) clan.Plataform);
                        cmd.ExecuteNonQuery();
                    }
                }

                // O clã mudou de tag...
                const string sqlUpdClan =
                    "update Main.Clan set ClanTag = @clanTag where (ClanId = @clanId) and (PlataformId = @plataformId);";
                using (var cmd = new SqlCommand(sqlUpdClan, t.Connection, t))
                {
                    cmd.CommandTimeout = 5 * 60;
                    cmd.Parameters.AddWithValue("@clanTag", clan.ClanTag);
                    cmd.Parameters.AddWithValue("@clanId", clan.ClanId);
                    cmd.Parameters.AddWithValue("@plataformId", (int) clan.Plataform);
                    cmd.ExecuteNonQuery();
                }

                // Isso será util para renomear os arquivos, posteriormente.
                clan.OldTag = databaseClanTag;
            }

            const string sqlSetClanName =
                "update Main.Clan set Name = @name where (ClanId = @clanId) and (PlataformId = @plataformId);";
            using (var cmd = new SqlCommand(sqlSetClanName, t.Connection, t))
            {
                cmd.CommandTimeout = 5 * 60;
                cmd.Parameters.AddWithValue("@name", clan.Name);
                cmd.Parameters.AddWithValue("@clanId", clan.ClanId);
                cmd.Parameters.AddWithValue("@plataformId", (int) clan.Plataform);
                cmd.ExecuteNonQuery();
            }

            using (var cmd = new SqlCommand("Main.SetClanDate", t.Connection, t))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 5 * 60;

                cmd.Parameters.AddWithValue("@PlataformId", (int) clan.Plataform);
                cmd.Parameters.AddWithValue("@ClanId", clan.ClanId);
                cmd.Parameters.AddWithValue("@ClanTag", clan.ClanTag);
                cmd.Parameters.AddWithValue("@Date", date);
                cmd.Parameters.AddWithValue("@MembershipMoment", clan.MembershipMoment);
                cmd.ExecuteNonQuery();
            }
            
            foreach (var player in clan.Players)
            {
                Set(player, t, !onlyMembership && player.Moment.Date == date);
            }

            // Apaga os jogadores para inserir novamente
            const string delHistSql = "delete from Main.ClanDatePlayer where (PlataformId = @PlataformId) and (ClanTag = @ClanTag) and ([Date] = @Date);";
            using (var cmd = new SqlCommand(delHistSql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 5 * 60;

                cmd.Parameters.AddWithValue("@PlataformId", (int)clan.Plataform);
                cmd.Parameters.AddWithValue("@ClanTag", clan.ClanTag);
                cmd.Parameters.AddWithValue("@Date", date);
                cmd.ExecuteNonQuery();
            }

            const string delCurrSql = "delete from [Current].ClanPlayer where (PlataformId = @PlataformId) and (ClanId = @ClanId);";
            using (var cmd = new SqlCommand(delCurrSql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 5 * 60;

                cmd.Parameters.AddWithValue("@PlataformId", (int)clan.Plataform);
                cmd.Parameters.AddWithValue("@ClanId", clan.ClanId);
                cmd.ExecuteNonQuery();
            }

            foreach (var player in clan.Players)
            {
                // Associa o jogador ao clã na data corrente
                const string sqlInsDatePlayer =
                    "insert into Main.ClanDatePlayer (PlataformId, ClanTag, [Date], PlayerId, RankId) " +
                    "values (@plataformId, @clanTag, @date, @playerId, @rankId);";
                using (var cmd = new SqlCommand(sqlInsDatePlayer, t.Connection, t))
                {
                    cmd.CommandTimeout = 5 * 60;
                    cmd.Parameters.AddWithValue("@plataformId", (int) player.Plataform);
                    cmd.Parameters.AddWithValue("@clanTag", clan.ClanTag);
                    cmd.Parameters.AddWithValue("@date", date);
                    cmd.Parameters.AddWithValue("@playerId", player.Id);
                    cmd.Parameters.AddWithValue("@rankId", (int) player.Rank);
                    cmd.ExecuteNonQuery();
                }

                const string sqlInsPlayer =
                    "insert into [Current].ClanPlayer (PlataformId, ClanId, PlayerId, RankId) " +
                    "values (@PlataformId, @ClanId, @PlayerId, @RankId);";
                using (var cmd = new SqlCommand(sqlInsPlayer, t.Connection, t))
                {
                    cmd.CommandTimeout = 5 * 60;
                    cmd.Parameters.AddWithValue("@PlataformId", (int)player.Plataform);
                    cmd.Parameters.AddWithValue("@ClanId", clan.ClanId);
                    cmd.Parameters.AddWithValue("@PlayerId", player.Id);
                    cmd.Parameters.AddWithValue("@RankId", (int)player.Rank);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Salva os valores esperados no BD
        /// </summary>
        public void Set(Wn8ExpectedValues ev)
        {
            Log.DebugFormat("Salvando Wn8 Expected Values BD...");
            var sw = Stopwatch.StartNew();
            Execute(transaction => { Set(ev, transaction); });
            Log.DebugFormat("Salva Wn8 Expected Values no BD em {0}.", sw.Elapsed);
        }

        private static void Set(Wn8ExpectedValues ev, SqlTransaction t)
        {
            DateTime now = DateTime.UtcNow;            

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

            using (var cmd = new SqlCommand("Tanks.SetWn8ExpectedValues", t.Connection, t))
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

            // Dispara o completamento da tabela
            using (var cmd = new SqlCommand("Tanks.CompleteWn8Expected", t.Connection, t))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 5 * 60;
                
                cmd.ExecuteNonQuery();
            }

        }

        public void Set(IEnumerable<TankPlayer> tankPlayers)
        {
            Log.DebugFormat("Salvando performance em tanques no BD...");
            var sw = Stopwatch.StartNew();
            Execute(transaction => { Set(tankPlayers.ToArray(), transaction); });
            Log.DebugFormat("Salva performance em tanques no BD em {0}.", sw.Elapsed);
        }

        [SuppressMessage("ReSharper", "LocalizableElement")]
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
                throw new ArgumentException("Only one player should be set at time.", nameof(tankPlayers));
            }

            bool firstTime;
            using (var cmd = new SqlCommand("Performance.HasAnyData", t.Connection, t))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 5 * 60;
                cmd.Parameters.AddWithValue("@plataformId", tps[0].Plataform);
                cmd.Parameters.AddWithValue("@playerId", tps[0].PlayerId);
                firstTime = (int) cmd.ExecuteScalar() == 0;
            }

            if (firstTime)
            {
                // Para que a contagem de mês funcione é preciso ancorar o que foi recém lido também no passado, de modo que a contagem inicie agora
                using (var cmd = new SqlCommand("Performance.SetPlayerDate", t.Connection, t))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandTimeout = 5 * 60;

                    foreach (var tp in tps)
                    {
                        cmd.Parameters.Clear();

                        DateTime anchorMoment = tp.LastBattle.AddYears(-1).AddMonths(-1);

                        cmd.Parameters.AddWithValue("@PlataformId", (int)tp.Plataform);
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
            }

            using (var cmd = new SqlCommand("Performance.SetPlayerDate", t.Connection, t))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 5 * 60;

                foreach (var tp in tps)
                {
                    cmd.Parameters.Clear();

                    cmd.Parameters.AddWithValue("@PlataformId", (int) tp.Plataform);
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

            using (var cmd = new SqlCommand("Achievements.SetPlayerMedal", t.Connection, t))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 5 * 60;

                // Loop for medals
                foreach (var tp in tps)
                {
                    if (tp.Achievements == null)
                    {
                        continue;
                    }

                    if (tp.Achievements.Count <= 0)
                    {
                        continue;
                    }

                    foreach (var medal in tp.Achievements)
                    {
                        cmd.Parameters.Clear();

                        cmd.Parameters.AddWithValue("@PlataformId", (int)tp.Plataform);
                        cmd.Parameters.AddWithValue("@PlayerId", tp.PlayerId);
                        cmd.Parameters.AddWithValue("@TankId", tp.TankId);
                        cmd.Parameters.AddWithValue("@MedalCode", medal.Key);
                        cmd.Parameters.AddWithValue("@Count", medal.Value);
                        
                        cmd.ExecuteNonQuery();
                    }
                }

                // Loop for ribbons
                foreach (var tp in tps)
                {
                    if (tp.Ribbons == null)
                    {
                        continue;
                    }

                    if (tp.Ribbons.Count <= 0)
                    {
                        continue;
                    }

                    foreach (var medal in tp.Ribbons)
                    {
                        cmd.Parameters.Clear();

                        cmd.Parameters.AddWithValue("@PlataformId", (int)tp.Plataform);
                        cmd.Parameters.AddWithValue("@PlayerId", tp.PlayerId);
                        cmd.Parameters.AddWithValue("@TankId", tp.TankId);
                        cmd.Parameters.AddWithValue("@MedalCode", medal.Key);
                        cmd.Parameters.AddWithValue("@Count", medal.Value);

                        cmd.ExecuteNonQuery();
                    }
                }
            }

        }

        public void AssociateDiscordUserToPlayer(long userId, long playerId)
        {
            Execute(t => AssociateDiscordUserToPlayer(userId, playerId, t));
        }

        private static void AssociateDiscordUserToPlayer(long userId, long playerId, SqlTransaction t)
        {
            using(var cmd = new SqlCommand("Discord.AssociateUserToPlayer", t.Connection, t))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@userId", userId);
                cmd.Parameters.AddWithValue("@playerId", playerId);
                cmd.ExecuteNonQuery();
            }
        }

        public void SetClanFlag(Platform platform, long clanId, string flagCode)
        {
            Execute(transaction => { SetClanFlag(platform, clanId, flagCode, transaction); });
        }

        private static void SetClanFlag(Platform platform, long clanId, string flagCode, SqlTransaction t)
        {
            const string sql =
                "update Main.Clan set FlagCode = @flagCode where (ClanId = @clanId) and (PlataformId = @plataformId);";
            using (var cmd = new SqlCommand(sql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 5 * 60;

                cmd.Parameters.AddWithValue("@clanId", clanId);
                cmd.Parameters.AddWithValue("@plataformId", (int)platform);
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
        }

        public void Set(IEnumerable<Tank> tanks)
        {
            Log.DebugFormat("Salvando tanques no BD...");
            var sw = Stopwatch.StartNew();
            Execute(transaction => { Set(tanks.ToArray(), transaction); });
            Log.DebugFormat("Salvos tanques no BD em {0}.", sw.Elapsed);
        }

        private static void Set(IEnumerable<Tank> tanks, SqlTransaction t)
        {
            using (var cmd = new SqlCommand("Tanks.SetTank", t.Connection, t))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 5 * 60;
                foreach (var tank in tanks)
                {
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@plataformId", (int) tank.Plataform);
                    cmd.Parameters.AddWithValue("@tankId", tank.TankId);
                    cmd.Parameters.AddWithValue("@name", tank.Name);
                    cmd.Parameters.AddWithValue("@shortName", tank.ShortName);
                    cmd.Parameters.AddWithValue("@nationId", (int) tank.Nation);
                    cmd.Parameters.AddWithValue("@tier", tank.Tier);
                    cmd.Parameters.AddWithValue("@typeId", (int) tank.Type);
                    cmd.Parameters.AddWithValue("@tag", tank.Tag);
                    cmd.Parameters.AddWithValue("@isPremium", tank.IsPremium);

                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        ///     Retorna a gamer tag de um jogador, ou null caso ele não exista
        /// </summary>
        private static string GetGamerTag(long playerId, SqlTransaction t)
        {
            const string sql = "select GamerTag from Main.Player where PlayerId = @playerId;";
            using (var cmd = new SqlCommand(sql, t.Connection, t))
            {
                cmd.CommandTimeout = 5 * 60;
                cmd.Parameters.AddWithValue("@playerId", playerId);
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

        /// <summary>
        ///     Retorna o nome do clã, ou nulo caso ele não exista
        /// </summary>
        private static string GetClanTag(Platform platform, long clanId, SqlTransaction t)
        {
            const string sql =
                "select ClanTag from Main.Clan where (PlataformId = @plataformId) and (ClanId = @clanId);";
            using (var cmd = new SqlCommand(sql, t.Connection, t))
            {
                cmd.CommandTimeout = 5 * 60;
                cmd.Parameters.AddWithValue("@plataformId", (int) platform);
                cmd.Parameters.AddWithValue("@clanId", clanId);
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
        public void CalculateReference(int utcShiftToCalculate = 0)
        {
            Log.Debug("Calculando Dados de Tanques...");
            var sw = Stopwatch.StartNew();
            Execute(t => CalculateReference(utcShiftToCalculate, t));
            Log.DebugFormat("Calculo de Dados de Tanques em {0}.", sw.Elapsed);
        }

        private static void CalculateMoE(int utcShiftToCalculate, SqlTransaction t)
        {
            using (var cmd = new SqlCommand("Performance.MoECalculatePercentile", t.Connection, t))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 50 * 60; // pode ser bastante lento!
                cmd.Parameters.AddWithValue("@utcShift", utcShiftToCalculate);
                cmd.ExecuteNonQuery();
            }
        }

        private static void CalculateReference(int utcShiftToCalculate, SqlTransaction t)
        {
            using (var cmd = new SqlCommand("Performance.CalculateReferenceValues", t.Connection, t))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 50 * 60; // Muitos minutos: pode ser bastante lento!
                cmd.Parameters.AddWithValue("@utcShift", utcShiftToCalculate);
                cmd.ExecuteNonQuery();
            }
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
            using (var cmd = new SqlCommand("Main.BalanceSchedule", t.Connection, t))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 5 * 60;
                cmd.Parameters.AddWithValue("@minPlayers", minPlayers);
                cmd.Parameters.AddWithValue("@maxPlayers", maxPlayers);
                cmd.ExecuteNonQuery();
            }
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
                Log.WarnFormat("Jogador {0}.{1}@{2} terá o nome truncado!", player.Id, player.Name, player.Plataform);
                player.Name = player.Name.Substring(0, 25);
            }

            if (databaseGamerTag == null)
            {
                // Caso um jogador não exista no sistema, crie
                const string sqlInsPlayer =
                    "insert into Main.Player (PlayerId, GamerTag, PlataformId) values (@playerId, @gamerTag, @plataformId);";
                using (var cmd = new SqlCommand(sqlInsPlayer, t.Connection, t))
                {
                    cmd.CommandTimeout = 5 * 60;
                    cmd.Parameters.AddWithValue("@playerId", player.Id);
                    cmd.Parameters.AddWithValue("@gamerTag", player.Name);
                    cmd.Parameters.AddWithValue("@plataformId", (int) player.Plataform);
                    cmd.ExecuteNonQuery();
                }
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
            Log.DebugFormat("Salvando calculos do clã {0}@{1} no BD em {2:yyyy-MM-dd}...", clan.ClanTag, clan.Plataform,
                clan.Date);
            var sw = Stopwatch.StartNew();
            Execute(transaction => { SetClanCalculation(clan, transaction); });
            Log.DebugFormat("Salvos calculos do clã {0}@{1} no BD em {2:yyyy-MM-dd} em {3}.", clan.ClanTag,
                clan.Plataform, clan.Date, sw.Elapsed);
        }

        private static void SetClanCalculation(Clan clan, SqlTransaction t)
        {
            if (clan.ClanId == 0)
            {
                throw new ArgumentException(@"The ClanId member can't be 0!", nameof(clan.ClanId));
            }

            using (var cmd = new SqlCommand("Main.SetClanCalculation", t.Connection, t))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 5 * 60;

                cmd.Parameters.AddWithValue("@PlataformId", (int)clan.Plataform);
                cmd.Parameters.AddWithValue("@ClanId", clan.ClanId);
                cmd.Parameters.AddWithValue("@ClanTag", clan.ClanTag);
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
        }

        /// <summary>
        ///     Adds the specified clan.
        /// </summary>
        public void Add(Clan clan)
        {
            Log.DebugFormat("Adicionando clã {3}.{0}@{1} no BD em {2:yyyy-MM-dd}...", clan.ClanTag, clan.Plataform,
                clan.Date, clan.ClanId);
            var sw = Stopwatch.StartNew();
            Execute(t => Add(clan, t));
            Log.DebugFormat("Adicionado clã {4}.{0}@{1} no BD em {2:yyyy-MM-dd} em {3}.", clan.ClanTag, clan.Plataform,
                clan.Date, sw.Elapsed, clan.ClanId);
        }

        private static void Add(Clan clan, SqlTransaction t)
        {
            using (var cmd = new SqlCommand("Main.AddClan", t.Connection, t))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 5 * 60;
                cmd.Parameters.AddWithValue("@plataformId", (int) clan.Plataform);
                cmd.Parameters.AddWithValue("@clanId", clan.ClanId);
                cmd.Parameters.AddWithValue("@clanTag", clan.ClanTag);
                cmd.Parameters.AddWithValue("@country",
                    string.IsNullOrWhiteSpace(clan.Country) ? (object) DBNull.Value : clan.Country.ToLowerInvariant());

                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        ///     Limpa a fila de clãs a serem adicionados ao sistema
        /// </summary>
        public void ClearClansToAddQueue()
        {
            Log.Debug("Limpando a fila de clãs a serem adicionados no BD...");
            var sw = Stopwatch.StartNew();
            Execute(ClearClansToAddQueue);
            Log.DebugFormat("Limpada a fila de clãs a serem adicionados no BD em {0}.", sw.Elapsed);
        }

        private static void ClearClansToAddQueue(SqlTransaction t)
        {
            const string sql = "delete from Main.ClanRequest;";
            using (var cmd = new SqlCommand(sql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.ExecuteNonQuery();
            }
        }

        public void EnableClan(Platform platform, long clanId)
        {
            Log.DebugFormat("Habilitando clã {0}.{1} no BD...", clanId, platform);
            var sw = Stopwatch.StartNew();
            Execute(t => EnableDisableClan(platform, clanId, true, DisabledReason.NotDisabled, t));
            Log.DebugFormat("Habilitado clã {0}.{1} no BD em {2}.", clanId, platform, sw.Elapsed);
        }

        public void DisableClan(Platform platform, long clanId, DisabledReason disabledReason)
        {
            Log.DebugFormat("Desabilitando clã {0}.{1} no BD por motivo {2}...", clanId, platform, disabledReason);
            var sw = Stopwatch.StartNew();
            Execute(t => EnableDisableClan(platform, clanId, false, disabledReason, t));
            Log.DebugFormat("Desabilitado clã {0}.{1} no BD em {2}.", clanId, platform, sw.Elapsed);
        }

        private static void EnableDisableClan(Platform platform, long clanId, bool enable,
            DisabledReason disabledReason, SqlTransaction t)
        {
            const string sql =
                "update Main.Clan set [Enabled] = @enableDisable, DisabledReason = @disabledReason where (ClanId = @clanId) and (PlataformId = @plataformId);";
            using (var cmd = new SqlCommand(sql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 5 * 60;

                cmd.Parameters.AddWithValue("@clanId", clanId);
                cmd.Parameters.AddWithValue("@plataformId", (int) platform);
                cmd.Parameters.AddWithValue("@enableDisable", enable);
                cmd.Parameters.AddWithValue("@disabledReason", (int) disabledReason);

                cmd.ExecuteNonQuery();
            }
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
            using (var cmd = new SqlCommand(sql, t.Connection, t))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 5 * 60;
                cmd.Parameters.AddWithValue("@maxToAutoAdd", maxToAutoAdd);
                cmd.ExecuteNonQuery();
            }
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
            using (var cmd = new SqlCommand(sql, t.Connection, t))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 5 * 60;
                cmd.Parameters.AddWithValue("@id", playerId);
                cmd.ExecuteNonQuery();
            }
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
            using (var cmd = new SqlCommand(sql, t.Connection, t))
            {
                cmd.CommandType = CommandType.StoredProcedure;

                foreach (var medal in medals)
                {
                    cmd.Parameters.Clear();

                    cmd.Parameters.AddWithValue("@PlataformId", (int) medal.Platform);
                    cmd.Parameters.AddWithValue("@MedalCode", medal.Code);
                    cmd.Parameters.AddWithValue("@Name", medal.Name);
                    cmd.Parameters.AddWithValue("@Description", (object) medal.Description ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@HeroInformation", (object) medal.HeroInformation ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Condition", (object) medal.Condition ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@CategoryId", (int)medal.Category);
                    cmd.Parameters.AddWithValue("@TypeId", (int)medal.Type);
                    cmd.Parameters.AddWithValue("@SectionId", (int)medal.Section);

                    cmd.ExecuteNonQuery();
                }
                
            }
        }
    }
}