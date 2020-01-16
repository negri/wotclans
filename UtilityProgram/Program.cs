using log4net;
using Negri.Wot;
using Negri.Wot.Sql;
using Negri.Wot.Tanks;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Negri.Wot.WgApi;
using Clan = Negri.Wot.Clan;
using Formatting = Newtonsoft.Json.Formatting;
using Tank = Negri.Wot.WgApi.Tank;

namespace UtilityProgram
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    internal class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Program));

        // Programa genérico, utilitário
        [SuppressMessage("ReSharper", "UnusedParameter.Local")]
        private static int Main(string[] args)
        {
            try
            {
                ExportResString();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex);
                return 1;
            }

            return 0;
        }

        /// <summary>
        /// Retrieve and populate all medals in the game
        /// </summary>
        private static void PopulateAllMedals()
        {
            var cacheDirectory = ConfigurationManager.AppSettings["CacheDirectory"];
            var fetcher = new Fetcher(cacheDirectory)
            {
                WebCacheAge = TimeSpan.FromMinutes(15),
                WebFetchInterval = TimeSpan.FromSeconds(1),
                ApplicationId = ConfigurationManager.AppSettings["WgApi"]
            };

            var gameMedals = fetcher.GetMedals(Platform.XBOX).ToDictionary(m => m.Code);
            var maxCode = gameMedals.Values.Max(m => m.Code.Length);
            var maxName = gameMedals.Values.Max(m => m.Name.Length);
            var maxDescription = gameMedals.Values.Max(m => m.Description.Length);
            var maxHeroInformation = gameMedals.Values.Max(m => m.HeroInformation?.Length ?? 0);
            var maxCondition = gameMedals.Values.Max(m => m.Condition?.Length ?? 0);

            var connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;
            var recorder = new DbRecorder(connectionString);
            recorder.Set(gameMedals.Values);

        }
    

        /// <summary>
        /// The rate a particular medal is won
        /// </summary>
        private static void CheckChiselMedalRate(string medalCode, long playerId)
        {
            var cacheDirectory = ConfigurationManager.AppSettings["CacheDirectory"];
            var fetcher = new Fetcher(cacheDirectory)
            {
                WebCacheAge = TimeSpan.FromMinutes(15),
                WebFetchInterval = TimeSpan.FromSeconds(1),
                ApplicationId = ConfigurationManager.AppSettings["WgApi"]
            };

            var player = fetcher.GetTanksForPlayer(Platform.XBOX, playerId, null, true).ToDictionary(t => t.TankId);
            var tanks = fetcher.GetTanks(Platform.XBOX).ToList();

            var eligibleTanks = tanks
                .Where(t => (t.Tier >= 8) && ((t.Type == TankType.Heavy) || (t.Type == TankType.Medium)) &&
                            ((t.Nation == Nation.Uk) || (t.Nation == Nation.Usa) || (t.Nation == Nation.Mercenaries)))
                .ToList();

            Log.Debug($"Stats for player {playerId} on the medal {medalCode}:");

            foreach (var tank in eligibleTanks.OrderBy(t => t.Tier).ThenBy(t => t.Type).ThenBy(t => t.Nation))
            {
                if (!player.TryGetValue(tank.TankId, out var tankPlayer))
                {
                    continue;
                }

                if (!tankPlayer.All.Achievements.TryGetValue(medalCode, out var numberOfDuelists))
                {
                    continue;
                }

                if (numberOfDuelists > 0)
                {
                    var rate = numberOfDuelists / (double)tankPlayer.All.Battles;
                    Log.Debug($"{tank.ShortName.PadRight(15, '.')}: {rate.ToString("P1").PadLeft(5)}, {numberOfDuelists.ToString("N0").PadLeft(5)} in {tankPlayer.All.Battles.ToString("N0").PadLeft(5)} battles");
                }
            }

        }

        /// <summary>
        /// The rate a particular medal is won
        /// </summary>
        private static void CheckChiselDuelistMedalRate(long playerId)
        {
            CheckChiselMedalRate("duelist", playerId);
        }

        private static void CheckChiselHighCaliberMedalRate(long playerId)
        {
            CheckChiselMedalRate("mainGun", playerId);
        }


        private static void PutPlayer(long playerId)
        {
            var connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;
            var provider = new DbProvider(connectionString);
            var player = provider.GetPlayer(playerId, true);
            player.Calculate(provider.GetWn8ExpectedValues(player.Plataform));

            var putter = new Putter("http://localhost:6094/", ConfigurationManager.AppSettings["ApiAdminKey"]);
            putter.Put(player);

            var ks = new KeyStore(connectionString);
            var savedPlayer = ks.GetPlayer(playerId);
        }

        private static void ExportResString()
        {
            var sb = new StringBuilder();

            const string file = @"C:\Projects\wotclans\WoTStat\Properties\Resources.resx";
            var doc = XDocument.Load(new XmlTextReader(new FileStream(file, FileMode.Open)));
            var root = doc.Root;

            if (root == null)
            {
                return;
            }

            foreach (var x in root.Descendants())
            {
                if (x.Name.LocalName != "data")
                {
                    continue;
                }

                var name = x.Attributes().FirstOrDefault(a => a.Name.LocalName == "name")?.Value ?? string.Empty;
                var value = x.Descendants().FirstOrDefault(d => d.Name.LocalName == "value")?.Value ?? string.Empty;

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                sb.AppendLine($"{name}$$${value}");
            }

            File.WriteAllText(@"c:\temp\Translation.txt", sb.ToString(), Encoding.UTF8);
        }

        private static void GetAllTanks(Platform platform)
        {
            var cacheDirectory = ConfigurationManager.AppSettings["CacheDirectory"];
            var fetcher = new Fetcher(cacheDirectory)
            {
                WebCacheAge = TimeSpan.FromMinutes(15),
                WebFetchInterval = TimeSpan.FromSeconds(1),
                ApplicationId = ConfigurationManager.AppSettings["WgApi"]
            };

            var tanks = fetcher.GetTanks(Platform.PC).ToArray();

            var connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;
            var recorder = new DbRecorder(connectionString);
            recorder.Set(tanks);
        }

        private static void CalculateAverageWn8OfAllTanks()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;
            var provider = new DbProvider(connectionString);

            var cd = DateTime.UtcNow.AddHours(-7);
            var previousMonday = cd.PreviousDayOfWeek(DayOfWeek.Monday);

            var references = provider.GetTanksReferences(Platform.XBOX, previousMonday, null, false, false, false).ToArray();

            var sb = new StringBuilder();
            sb.AppendLine("Id\tName\tIsPremium\tTier\tType\tNumPlayers\tNumBattles\tAvgWN8\tDamageToUnicum");
            foreach (var t in references)
            {
                sb.AppendLine(
                    $"{t.TankId}\t{t.Name}\t{t.IsPremium}\t{t.Tier}\t{t.Type}\t{t.TotalPlayers}\t{t.TotalBattles}\t{t.AverageWn8:N0}\t{t.TargetDamageUnicum:N0}");
            }

            File.WriteAllText("C:\\temp\\References.txt", sb.ToString(), Encoding.UTF8);
        }

        private static void TestWN8Calculation()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;
            var provider = new DbProvider(connectionString);
            var expected = provider.GetWn8ExpectedValues(Platform.XBOX);

            var fakePlayed = new Dictionary<long, TankPlayerStatistics>();
            foreach (var e in expected.AllTanks)
            {
                fakePlayed.Add(e.TankId,
                    new TankPlayerStatistics
                    {
                        Battles = 100000,
                        DamageDealt = (long)(e.Damage * 100000),
                        Wins = (long)(e.WinRate * 100000),
                        Kills = (long)(e.Frag * 100000),
                        Spotted = (long)(e.Spot * 100000),
                        DroppedCapturePoints = (long)(e.Def * 100000)
                    });
            }

            // Teste do geral
            var wn8 = expected.CalculateWn8(fakePlayed);
            Log.Info($"WN8 de Referência: {wn8} - Deve ser proximo de 1565");

            // Teste de um jogador (eu!)
            var sw = Stopwatch.StartNew();
            var p = provider.GetPlayer(1763298, true);
            p.Calculate(expected);
            sw.Stop();
            Log.Info($"All:   {p.TotalWn8:N0} on {p.TotalBattles:N0} @ {p.TotalWinRate:P2} on Tier {p.TotalTier:N1}");
            Log.Info($"Month: {p.MonthWn8:N0} on {p.MonthBattles:N0} @ {p.MonthWinRate:P2} on Tier {p.MonthTier:N1}");
            Log.Info($"Week:  {p.WeekWn8:N0} on {p.WeekBattles:N0} @ {p.WeekWinRate:P2} on Tier {p.WeekTier:N1}");
            Log.Debug($"In {sw.Elapsed.TotalMilliseconds:N0}ms");

            foreach (var t in p.Performance.Month)
            {
                var td = expected[t.Key];
                if (td.Tier < 10)
                {
                    continue;
                }

                Log.Debug($"{td.Name}: {t.Value.Wn8:N0}");
            }

            // Teste de dano esperado para um tanque qualquer (T110E5)
            sw = Stopwatch.StartNew();
            var te = expected[10785];
            var damageAverage = te.GetTargetDamage(Wn8Rating.Average);
            var damageGood = te.GetTargetDamage(Wn8Rating.Good);
            var damageGreat = te.GetTargetDamage(Wn8Rating.Great);
            var damageUnicum = te.GetTargetDamage(Wn8Rating.Unicum);
            sw.Stop();
            Log.Debug($"Target Damages em {sw.Elapsed.TotalMilliseconds:N1}ms: {damageAverage:N0}; {damageGood:N0}; {damageGreat:N0}; {damageUnicum:N0}");
        }

        /// <summary>
        ///     Dispara a deleção de arquivos no servidor usando o método de API
        /// </summary>
        private static void DeleteOldFileOnServer()
        {
            var cleanerXbox = new Putter(Platform.XBOX, ConfigurationManager.AppSettings["ApiAdminKey"]);
            cleanerXbox.CleanFiles();

            var cleanerPs = new Putter(Platform.PS, ConfigurationManager.AppSettings["ApiAdminKey"]);
            cleanerPs.CleanFiles();
        }

        /// <summary>
        ///     Exporta o WN8 esperado
        /// </summary>
        public static void CalculateWn8Expected()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;
            var provider = new DbProvider(connectionString);

            var putter = new FtpPutter(ConfigurationManager.AppSettings["PsFtpFolder"],
                ConfigurationManager.AppSettings["PsFtpUser"],
                ConfigurationManager.AppSettings["PsFtpPassworld"]);

            var resultDirectory = ConfigurationManager.AppSettings["PsResultDirectory"];

            var wn8 = provider.GetWn8ExpectedValues(Platform.PS);
            if (wn8 != null)
            {
                var json = JsonConvert.SerializeObject(wn8, Formatting.Indented);
                var file = Path.Combine(resultDirectory, "MoE", $"{wn8.Date:yyyy-MM-dd}.WN8.json");
                File.WriteAllText(file, json, Encoding.UTF8);
                Log.DebugFormat("Salvo o WN8 Expected em '{0}'", file);

                putter.PutMoe(file);
                Log.Debug("Feito uploado do WN8");
            }
        }

        private static void DeleteOldFiles(int daysToKeepClans, int daysToKeepTanks)
        {
            var putterXbox = new FtpPutter(ConfigurationManager.AppSettings["FtpFolder"],
                ConfigurationManager.AppSettings["FtpUser"],
                ConfigurationManager.AppSettings["FtpPassworld"]);

            var putterPs = new FtpPutter(ConfigurationManager.AppSettings["PsFtpFolder"],
                ConfigurationManager.AppSettings["PsFtpUser"],
                ConfigurationManager.AppSettings["PsFtpPassworld"]);

            var deleted = putterXbox.DeleteOldFiles(daysToKeepTanks, "Tanks");
            deleted += putterXbox.DeleteOldFiles(daysToKeepClans);
            Log.InfoFormat($"Deletados {deleted} do XBOX");

            deleted = putterPs.DeleteOldFiles(daysToKeepClans);
            deleted += putterPs.DeleteOldFiles(daysToKeepTanks, "Tanks");
            Log.InfoFormat($"Deletados {deleted} do PS");
        }

        /// <summary>
        ///     Calcula todos os clas habilitados e os salva
        /// </summary>
        private static void CalculateAllClans()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;
            var provider = new DbProvider(connectionString);
            var clans = provider.GetClans().ToArray();
            Log.InfoFormat("{0} clas devem ser calculados.", clans.Length);

            var recorder = new DbRecorder(connectionString);

            var putterXbox = new FtpPutter(ConfigurationManager.AppSettings["FtpFolder"],
                ConfigurationManager.AppSettings["FtpUser"],
                ConfigurationManager.AppSettings["FtpPassworld"]);

            var putterPs = new FtpPutter(ConfigurationManager.AppSettings["PsFtpFolder"],
                ConfigurationManager.AppSettings["PsFtpUser"],
                ConfigurationManager.AppSettings["PsFtpPassworld"]);

            var resultDirectory = ConfigurationManager.AppSettings["ResultDirectory"];
            var resultDirectoryPs = ConfigurationManager.AppSettings["PsResultDirectory"];


            var already = new HashSet<string>(File.ReadAllLines(Path.Combine(resultDirectory, "CalcTask.txt")));
            var alreadyPs = new HashSet<string>(File.ReadAllLines(Path.Combine(resultDirectoryPs, "CalcTask.txt")));

            var o = new object();

            // Calcula cada cla
            var doneCount = 0;
            var sw = Stopwatch.StartNew();
            Parallel.For(0, clans.Length, new ParallelOptions { MaxDegreeOfParallelism = 2 }, i =>
              {
                  var clan = clans[i];

                  var done = false;
                  if (clan.Plataform == Platform.XBOX)
                  {
                      done = already.Contains(clan.ClanTag);
                  }
                  else if (clan.Plataform == Platform.PS)
                  {
                      done = alreadyPs.Contains(clan.ClanTag);
                  }

                  if (done)
                  {
                      Log.InfoFormat("cla {0} de {1}: {2}@{3} feito anteriormente.", i + 1, clans.Length, clan.ClanTag, clan.Plataform);
                      Interlocked.Increment(ref doneCount);
                      return;
                  }

                  Log.InfoFormat("Processando cla {0} de {1}: {2}@{3}...", i + 1, clans.Length, clan.ClanTag,
                      clan.Plataform);
                  var csw = Stopwatch.StartNew();

                  var cc = CalculateClan(clan, provider, recorder);

                  Log.InfoFormat("Calculado cla {0} de {1}: {2}@{3} em {4:N1}s...",
                      i + 1, clans.Length, clan.ClanTag, clan.Plataform, csw.Elapsed.TotalSeconds);

                  if (cc != null)
                  {
                      var fsw = Stopwatch.StartNew();
                      switch (cc.Plataform)
                      {
                          case Platform.XBOX:
                              {
                                  var fileName = cc.ToFile(resultDirectory);
                                  Log.InfoFormat("Arquivo de resultado escrito em '{0}'", fileName);
                                  putterXbox.PutClan(fileName);
                                  lock (o)
                                  {
                                      File.AppendAllText(Path.Combine(resultDirectory, "CalcTask.txt"), $"{cc.ClanTag}\r\n", Encoding.UTF8);
                                  }
                              }
                              break;
                          case Platform.PS:
                              {
                                  var fileName = cc.ToFile(resultDirectoryPs);
                                  Log.InfoFormat("Arquivo de resultado escrito em '{0}'", fileName);
                                  putterPs.PutClan(fileName);
                                  lock (o)
                                  {
                                      File.AppendAllText(Path.Combine(resultDirectoryPs, "CalcTask.txt"), $"{cc.ClanTag}\r\n", Encoding.UTF8);
                                  }
                              }
                              break;
                          case Platform.Virtual:
                              break;
                          default:
                              throw new ArgumentOutOfRangeException();
                      }

                      Log.InfoFormat("Upload do cla {0} de {1}: {2}@{3} em {4:N1}s...",
                          i + 1, clans.Length, clan.ClanTag, clan.Plataform, fsw.Elapsed.TotalSeconds);
                  }

                  Interlocked.Increment(ref doneCount);
                  Log.InfoFormat("Processado cla {0} de {1}: {2}@{3} em {4:N1}s. {5} totais.",
                      i + 1, clans.Length, clan.ClanTag, clan.Plataform, csw.Elapsed.TotalSeconds, doneCount);
              });
            var calculationTime = sw.Elapsed;
        }

        private static Clan CalculateClan(ClanPlataform clan, DbProvider provider,
            DbRecorder recorder)
        {
            Log.DebugFormat("Calculando cla {0}@{1}...", clan.ClanTag, clan.Plataform);

            var cc = provider.GetClan(clan.Plataform, clan.ClanId);

            if (cc == null)
            {
                Log.Warn("O cla ainda não teve nenhum membro atualizado.");
                return null;
            }

            if (cc.Count == 0)
            {
                Log.Warn("O cla ainda não teve nenhum membro atualizado.");
                return null;
            }

            Log.InfoFormat("------------------------------------------------------------------");
            Log.InfoFormat("cla:                     {0}@{1}", cc.ClanTag, cc.Plataform);
            Log.InfoFormat("# Membros:               {0};{1};{2} - Patched: {3}", cc.Count, cc.Active, 0,
                cc.NumberOfPatchedPlayers);
            Log.InfoFormat("Batalhas:                T:{0:N0};A:{1:N0};W:{2:N0}", cc.TotalBattles, cc.ActiveBattles,
                0);

            recorder.SetClanCalculation(cc);

            return cc;
        }

        private static void GetSiteDiagnostic()
        {
            var webCacheAge = TimeSpan.FromMinutes(10);
            var cacheDirectory = ConfigurationManager.AppSettings["CacheDirectory"];
            var fetcher = new Fetcher(cacheDirectory)
            {
                WebCacheAge = webCacheAge,
                WebFetchInterval = TimeSpan.FromSeconds(1),
                ApplicationId = ConfigurationManager.AppSettings["WgApi"]
            };
            var si = fetcher.GetSiteDiagnostic("https://wotclans.com.br/api/status", ConfigurationManager.AppSettings["ApiAdminKey"]);
        }

        #region Valores Esperados de WN8

        private static void GetXvmWn8()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;
            var provider = new DbProvider(connectionString);
            var recorder = new DbRecorder(connectionString);

            var webCacheAge = TimeSpan.FromMinutes(10);
            var cacheDirectory = ConfigurationManager.AppSettings["CacheDirectory"];

            var fetcher = new Fetcher(cacheDirectory)
            {
                WebCacheAge = webCacheAge,
                WebFetchInterval = TimeSpan.FromSeconds(1),
                ApplicationId = ConfigurationManager.AppSettings["WgApi"]
            };

            var data = provider.EnumTanks(Platform.XBOX).ToArray();
        }

        #endregion

        #region Dados de WoTStatConsole

        private static void GetFromWoTStatConsole()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;
            var provider = new DbProvider(connectionString);
            var recorder = new DbRecorder(connectionString);

            var webCacheAge = TimeSpan.FromMinutes(10);
            var cacheDirectory = ConfigurationManager.AppSettings["CacheDirectory"];

            var fetcher = new Fetcher(cacheDirectory)
            {
                WebCacheAge = webCacheAge,
                WebFetchInterval = TimeSpan.FromSeconds(1),
                ApplicationId = ConfigurationManager.AppSettings["WgApi"]
            };

            Log.Debug("Obtendo jogadores a atualizar.");
            const int ageHours = 24;
            var players = provider.GetPlayersUpdateOrder(1000, ageHours).Where(p => p.AdjustedAgeHours < 1000).Take(1000).ToArray();

            if (players.Length <= 0)
            {
                Log.Warn("Fila vazia!");
                return;
            }

            Log.Info($"{players.Length} na fila.");

            var sw = Stopwatch.StartNew();

            var cts = new CancellationTokenSource();
            var po = new ParallelOptions
            {
                CancellationToken = cts.Token,
                MaxDegreeOfParallelism = 2
            };

            var isEnd = false;
            var count = 0;
            Parallel.For(0, players.Length, po, i =>
            {
                if (isEnd)
                {
                    return;
                }

                if (sw.Elapsed.TotalMinutes > 10)
                {
                    isEnd = true;

                    Log.Warn("Tempo Esgotado!");
                    Log.Info($"Completo! {count} em {sw.Elapsed.TotalSeconds}. {count / sw.Elapsed.TotalMinutes:N1} players/minute");
                    return;
                }

                var player = players[i];
                var task = fetcher.GetPlayerWn8Async(player);
                task.Wait(po.CancellationToken);

                var completePlayer = task.Result;
                if (completePlayer != null)
                {
                    if (completePlayer.CanSave())
                    {
                        recorder.Set(completePlayer);
                        Log.Info("Salvo!");
                    }
                    else
                    {
                        Log.WarnFormat("Jogador {0}.{1}@{2} com muitos dados zerados não será salvo no BD.",
                            completePlayer.Id, completePlayer.Name, completePlayer.Plataform);
                    }
                }

                Interlocked.Increment(ref count);
            });
            sw.Stop();

            Log.Info($"Completo! {count} em {sw.Elapsed.TotalSeconds}. {count / sw.Elapsed.TotalMinutes:N1} players/minute");
        }

        #endregion

        #region Referencias

        private static void DumpReferenceFiles()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;
            var provider = new DbProvider(connectionString);


            var references = provider.GetTanksReferences(Platform.PS, new DateTime(2018, 03, 12))
                .ToArray();
            var baseDir = ConfigurationManager.AppSettings["PsResultDirectory"];
            var dir = Path.Combine(baseDir, "Tanks");

            var leaders = new List<Leader>();
            foreach (var r in references)
            {
                r.Save(dir);
                Log.InfoFormat("Escrito {0}", r.Name);
                leaders.AddRange(r.Leaders);
            }

            var json = JsonConvert.SerializeObject(leaders, Formatting.Indented);
            var file = Path.Combine(dir, $"{references.First().Date:yyyy-MM-dd}.Leaders.json");
            File.WriteAllText(file, json, Encoding.UTF8);

            var sb = new StringBuilder();
            foreach (var leader in leaders)
            {
                sb.AppendLine(leader.ToString());
            }

            file = Path.Combine(dir, $"{references.First().Date:yyyy-MM-dd}.Leaders.txt");
            File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
        }

        #endregion

        #region MoE

        private static void DumpMoEFiles(string[] args)
        {
            var connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;
            var provider = new DbProvider(connectionString);

            const Platform plataform = Platform.XBOX;

            var date = new DateTime(2017, 03, 10);
            var maxDate = new DateTime(2017, 04, 26);

            while (date <= maxDate)
            {
                var moes = provider.GetMoe(plataform, date).ToDictionary(t => t.TankId);
                var dateOnDb = moes.First().Value.Date;

                var json = JsonConvert.SerializeObject(moes, Formatting.Indented);

                var baseDir = ConfigurationManager.AppSettings["ResultsFolder"];
                var file = Path.Combine(baseDir, "MoE", $"{dateOnDb:yyyy-MM-dd}.moe.json");
                File.WriteAllText(file, json, Encoding.UTF8);

                date = date.AddDays(1.0);

                Log.InfoFormat("Escrito {0}", file);
            }
        }

        #endregion

        #region Pegar dados de Tanques

        private static void GetAllTanks(string[] args)
        {
            var fetcher = new Fetcher(ConfigurationManager.AppSettings["CacheDirectory"])
            {
                WebFetchInterval = TimeSpan.FromSeconds(5),
                ApplicationId = ConfigurationManager.AppSettings["WgApi"]
            };

            var topCount = 20;
            if (args.Length >= 1)
            {
                topCount = int.Parse(args[0]);
            }

            Log.InfoFormat("Top Count: {0}", topCount);

            var dir = "c:\\Projects\\wotclans\\TopTanks";
            if (args.Length >= 2)
            {
                dir = args[2];
            }

            Log.InfoFormat("Directory: {0}", dir);

            // Obtem todos os tanques do jogo       
            Log.Debug("Obtendo todos os tanques do jogo...");
            var allTanks = fetcher.GetTanks(Platform.XBOX).ToDictionary(t => t.TankId);
            var sb = new StringBuilder();
            foreach (var tank in allTanks.Values)
            {
                sb.Append(
                    $"{tank.TankId}\t{tank.Name}\t{tank.Images["big_icon"]}\t{tank.IsPremium}\t{tank.NationString}\t{tank.ShortName}\t{tank.Tag}\t{tank.Tier}\t{tank.TypeString}\r\n");
            }

            var tanksFile = $"{dir}\\AllTanks.{DateTime.Today:yyyy-MM-dd}.txt";
            File.WriteAllText(tanksFile, sb.ToString(), Encoding.UTF8);
            Log.InfoFormat("Salvos {0} tanques em {1}", allTanks.Count, tanksFile);

            var connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;
            var provider = new DbProvider(connectionString);

            // Lista todos os clas XBOX
            var allClans =
                provider.GetClans().Where(c => c.Plataform == Platform.XBOX).Select(cp => provider.GetClan(cp))
                    .OrderByDescending(c => c.Top15Wn8).ToArray();
            Log.InfoFormat("Obtidos {0} clas.", allClans.Length);

            // Seleciona a amostragem
            var topClans = allClans.Take(topCount).ToArray();
            var topPlayersCount = topClans.Sum(c => c.Active);

            var i = allClans.Length - 1;
            var bottonClans = new List<Clan>();
            while (bottonClans.Sum(c => c.Active) < topPlayersCount && i >= 0)
            {
                bottonClans.Add(allClans[i--]);
            }


            var clanTags = new[]
            {
                "ETBR", "UNOT", "BOPBR", "BK", "FERAS", "BR", "VIS", "171BS", "GDAB3", "TCF",
                "DV", "TOPBR", "OWS", "AR-15", "DBD", "NT", "ITA", "13RCM", "BOPE", "-RSA-"
            };
            foreach (var clanTag in topClans)
            {
                Console.WriteLine(@"cla {0}...", clanTag.Name);
                File.Delete($"{dir}\\AllStats.{clanTag}.{DateTime.Today:yyyy-MM-dd}.txt");

                var cc = clanTag;

                foreach (var player in cc.Players)
                {
                    sb = new StringBuilder();


                    Console.WriteLine(@"    Jogador {0}.{1}@{2}...", player.Id, player.Name, clanTag);

                    var tanksTask = fetcher.GetTanksForPlayerAsync(Platform.XBOX, player.Id);
                    tanksTask.Wait();
                    var tanks = tanksTask.Result.ToArray();
                    foreach (var t in tanks)
                    {
                        if (!allTanks.TryGetValue(t.TankId, out var td))
                        {
                            td = new Tank { ShortName = "???", Tier = 0, TypeString = "???" };
                        }

                        sb.Append(
                            $"{cc.ClanId}\t{cc.ClanTag}\t{cc.Top15Wn8}\t{player.Id}\t{player.Name}\t{player.Rank}\t{player.MonthWinRate}\t{player.MonthWn8}\t{player.MonthBattles}\t");
                        sb.Append($"{t.TankId}\t{td.ShortName}\t{td.Tier}\t{td.Type}\t");
                        sb.Append(
                            $"{t.BattleLifeTime.TotalMinutes:0}\t{t.LastBattle:yyyy-MM-dd}\t{t.MaxFrags}\t{t.MarkOfMastery}\t");
                        sb.Append(
                            $"{t.All.Battles}\t{t.All.Wins}\t{t.All.Kills}\t{t.All.SurvivedBattles}\t{t.All.DamageDealt}\t{t.All.DamageAssisted}\t{t.All.DamageReceived}");
                        sb.AppendLine();

                        //Console.WriteLine(@"        Tanque {0}.{1}...", t.TankId, td.ShortName);
                    }

                    File.AppendAllText($"{dir}\\AllStats.{clanTag}.{DateTime.Today:yyyy-MM-dd}.txt", sb.ToString(),
                        Encoding.UTF8);
                }
            }
        }

        #endregion

        #region Pegar dados de clas

        private static void ListClans(int size)
        {
            var fetcher = new Fetcher(@"C:\Projects\wotclans\Cache")
            {
                WebFetchInterval = TimeSpan.FromSeconds(1),
                ApplicationId = ConfigurationManager.AppSettings["WgApi"]
            };
            var clans = fetcher.GetClans(Platform.XBOX, size).ToArray();

            var provider = new DbProvider(ConfigurationManager.ConnectionStrings["Main"].ConnectionString);

            var newClans = 0;
            foreach (var clan in clans)
            {
                Console.Write(@"{0} ({1}). Já existe? ", clan.ClanTag, clan.ClanId);
                var existingClan = provider.GetClan(clan);
                Console.WriteLine(@"{0}", existingClan == null ? " não" : "Sim");
                if (existingClan == null)
                {
                    newClans++;
                }
            }

            Console.WriteLine(@"{0} novos clas!", newClans);

            Console.ReadLine();
        }

        /// <summary>
        ///     Lista arquivos no diretorio FTP
        /// </summary>
        private static void ListFiles()
        {
            var putter = new FtpPutter(ConfigurationManager.AppSettings["FtpFolder"],
                ConfigurationManager.AppSettings["FtpUser"],
                ConfigurationManager.AppSettings["FtpPassworld"]);
            var remoteClanFiles = putter.List("clan.TERSP.");
            foreach (var remoteClanFile in remoteClanFiles)
            {
                putter.DeleteFile(remoteClanFile);
            }
        }

        /// <summary>
        ///     Obtem todos os clas do jogo
        /// </summary>
        private static void GetAllClans()
        {
            var ids = new List<string>();

            using (var client = new HttpClient())
            {
                // Numero de Páginas a serem lidas
                var lastSize = 100;
                for (var i = 1; i <= 62 && lastSize >= 7; ++i)
                {
                    Console.WriteLine("Consultando página {0}...", i);

                    var urlRequest =
                        $"https://api-xbox-console.worldoftanks.com/wotx/clans/list/?application_id=demo&fields=clan_id%2Ctag%2Cname%2Cmembers_count&page_no={i}";
                    var result = client.GetAsync(urlRequest).Result;
                    var json = result.Content.ReadAsStringAsync().Result;
                    var clanInfoResult = JsonConvert.DeserializeObject<ClanInfoResult>(json);
                    if (clanInfoResult.Status == "ok" && clanInfoResult.Meta.Count >= 1)
                    {
                        foreach (var c in clanInfoResult.Data)
                        {
                            ids.Add($"{c.Tag}\t{c.Name}\t{c.ClanId}\t{c.MembersCount}");
                            Console.WriteLine($"    {c.Tag} - {c.ClanId} - {c.MembersCount}");
                            lastSize = c.MembersCount;
                        }
                    }
                }
            }

            const string resultFile = @"C:\Projects\wotclans\GetAllClanIds\AllClans.txt";
            File.Delete(resultFile);
            File.AppendAllLines(resultFile, ids, Encoding.UTF8);
        }

        private static void GetIds()
        {
            const string clansToIdFile = @"C:\Projects\wotclans\GetAllClanIds\ClansToId.txt";
            var clansTags = File.ReadAllLines(clansToIdFile, Encoding.UTF8).Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
            var ids = new List<string>();

            using (var client = new HttpClient())
            {
                foreach (var tag in clansTags)
                {
                    var urlRequest =
                        $"https://api-xbox-console.worldoftanks.com/wotx/clans/list/?application_id=demo&search={tag}&limit=1&fields=clan_id%2Ctag";
                    var result = client.GetAsync(urlRequest).Result;
                    var json = result.Content.ReadAsStringAsync().Result;
                    var clanInfoResult = JsonConvert.DeserializeObject<ClanInfoResult>(json);
                    if (clanInfoResult.Status == "ok" && clanInfoResult.Meta.Count == 1 &&
                        clanInfoResult.Data[0].Tag == tag)
                    {
                        ids.Add($"{tag}\t{clanInfoResult.Data[0].ClanId}");
                    }
                }
            }

            const string resultFile = @"C:\Projects\wotclans\GetAllClanIds\ClansIds.txt";
            File.Delete(resultFile);
            File.AppendAllLines(resultFile, ids, Encoding.UTF8);
        }
    }

    public class ClanInfo
    {
        [JsonProperty("clan_id")]
        public long ClanId { get; set; }

        public string Tag { get; set; }

        public string Name { get; set; }

        [JsonProperty("members_count")]
        public int MembersCount { get; set; }
    }

    public class MetaData
    {
        public long Count { get; set; }

        public long Total { get; set; }
    }

    public class ClanInfoResult
    {
        public string Status { get; set; }

        public MetaData Meta { get; set; }

        public ClanInfo[] Data { get; set; } = new ClanInfo[0];
    }

    #endregion
}