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
                CalculateAverageWn8OfAllTanks();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex);
                return 1;
            }

            return 0;
        }

        private static void PutPlayer(long playerId)
        {
            string connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;
            DbProvider provider = new DbProvider(connectionString);
            Player player = provider.GetPlayer(playerId, true);
            player.Calculate(provider.GetWn8ExpectedValues(player.Plataform));

            Putter putter = new Putter("http://localhost:6094/", ConfigurationManager.AppSettings["ApiAdminKey"]);
            putter.Put(player);

            KeyStore ks = new KeyStore(connectionString);
            Player savedPlayer = ks.GetPlayer(playerId);
        }

        private static void ExportResString()
        {
            StringBuilder sb = new StringBuilder();

            const string file = @"C:\Projects\wotclans\WoTStat\Properties\Resources.resx";
            XDocument doc = XDocument.Load(new XmlTextReader(new FileStream(file, FileMode.Open)));
            XElement root = doc.Root;

            if (root == null)
            {
                return;
            }

            foreach (XElement x in root.Descendants())
            {
                if (x.Name.LocalName != "data")
                {
                    continue;
                }

                string name = x.Attributes().FirstOrDefault(a => a.Name.LocalName == "name")?.Value ?? string.Empty;
                string value = x.Descendants().FirstOrDefault(d => d.Name.LocalName == "value")?.Value ?? string.Empty;

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
            string cacheDirectory = ConfigurationManager.AppSettings["CacheDirectory"];
            Fetcher fetcher = new Fetcher(cacheDirectory)
            {
                WebCacheAge = TimeSpan.FromMinutes(15),
                WebFetchInterval = TimeSpan.FromSeconds(1),
                ApplicationId = ConfigurationManager.AppSettings["WgApi"]
            };

            Tank[] tanks = fetcher.GetTanks(Platform.PC).ToArray();

            string connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;
            DbRecorder recorder = new DbRecorder(connectionString);
            recorder.Set(tanks);
        }

        private static void CalculateAverageWn8OfAllTanks()
        {
            string connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;
            DbProvider provider = new DbProvider(connectionString);

            DateTime cd = DateTime.UtcNow.AddHours(-7);
            DateTime previousMonday = cd.PreviousDayOfWeek(DayOfWeek.Monday);

            TankReference[] references = provider.GetTanksReferences(Platform.XBOX, previousMonday, null, false, false, false).ToArray();

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
            string connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;
            DbProvider provider = new DbProvider(connectionString);
            Wn8ExpectedValues expected = provider.GetWn8ExpectedValues(Platform.XBOX);

            Dictionary<long, TankPlayerStatistics> fakePlayed = new Dictionary<long, TankPlayerStatistics>();
            foreach (Wn8TankExpectedValues e in expected.AllTanks)
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
            double wn8 = expected.CalculateWn8(fakePlayed);
            Log.Info($"WN8 de Referência: {wn8} - Deve ser proximo de 1565");

            // Teste de um jogador (eu!)
            Stopwatch sw = Stopwatch.StartNew();
            Player p = provider.GetPlayer(1763298, true);
            p.Calculate(expected);
            sw.Stop();
            Log.Info($"All:   {p.TotalWn8:N0} on {p.TotalBattles:N0} @ {p.TotalWinRate:P2} on Tier {p.TotalTier:N1}");
            Log.Info($"Month: {p.MonthWn8:N0} on {p.MonthBattles:N0} @ {p.MonthWinRate:P2} on Tier {p.MonthTier:N1}");
            Log.Info($"Week:  {p.WeekWn8:N0} on {p.WeekBattles:N0} @ {p.WeekWinRate:P2} on Tier {p.WeekTier:N1}");
            Log.Debug($"In {sw.Elapsed.TotalMilliseconds:N0}ms");

            foreach (KeyValuePair<long, TankPlayerStatistics> t in p.Performance.Month)
            {
                Wn8TankExpectedValues td = expected[t.Key];
                if (td.Tier < 10)
                {
                    continue;
                }

                Log.Debug($"{td.Name}: {t.Value.Wn8:N0}");
            }

            // Teste de dano esperado para um tanque qualquer (T110E5)
            sw = Stopwatch.StartNew();
            Wn8TankExpectedValues te = expected[10785];
            double damageAverage = te.GetTargetDamage(Wn8Rating.Average);
            double damageGood = te.GetTargetDamage(Wn8Rating.Good);
            double damageGreat = te.GetTargetDamage(Wn8Rating.Great);
            double damageUnicum = te.GetTargetDamage(Wn8Rating.Unicum);
            sw.Stop();
            Log.Debug($"Target Damages em {sw.Elapsed.TotalMilliseconds:N1}ms: {damageAverage:N0}; {damageGood:N0}; {damageGreat:N0}; {damageUnicum:N0}");
        }

        /// <summary>
        ///     Dispara a deleção de arquivos no servidor usando o método de API
        /// </summary>
        private static void DeleteOldFileOnServer()
        {
            Putter cleanerXbox = new Putter(Platform.XBOX, ConfigurationManager.AppSettings["ApiAdminKey"]);
            cleanerXbox.CleanFiles();

            Putter cleanerPs = new Putter(Platform.PS, ConfigurationManager.AppSettings["ApiAdminKey"]);
            cleanerPs.CleanFiles();
        }

        /// <summary>
        ///     Exporta o WN8 esperado
        /// </summary>
        public static void CalculateWn8Expected()
        {
            string connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;
            DbProvider provider = new DbProvider(connectionString);

            FtpPutter putter = new FtpPutter(ConfigurationManager.AppSettings["PsFtpFolder"],
                ConfigurationManager.AppSettings["PsFtpUser"],
                ConfigurationManager.AppSettings["PsFtpPassworld"]);

            string resultDirectory = ConfigurationManager.AppSettings["PsResultDirectory"];

            Wn8ExpectedValues wn8 = provider.GetWn8ExpectedValues(Platform.PS);
            if (wn8 != null)
            {
                string json = JsonConvert.SerializeObject(wn8, Formatting.Indented);
                string file = Path.Combine(resultDirectory, "MoE", $"{wn8.Date:yyyy-MM-dd}.WN8.json");
                File.WriteAllText(file, json, Encoding.UTF8);
                Log.DebugFormat("Salvo o WN8 Expected em '{0}'", file);

                putter.PutMoe(file);
                Log.Debug("Feito uploado do WN8");
            }
        }

        private static void DeleteOldFiles(int daysToKeepClans, int daysToKeepTanks)
        {
            FtpPutter putterXbox = new FtpPutter(ConfigurationManager.AppSettings["FtpFolder"],
                ConfigurationManager.AppSettings["FtpUser"],
                ConfigurationManager.AppSettings["FtpPassworld"]);

            FtpPutter putterPs = new FtpPutter(ConfigurationManager.AppSettings["PsFtpFolder"],
                ConfigurationManager.AppSettings["PsFtpUser"],
                ConfigurationManager.AppSettings["PsFtpPassworld"]);

            int deleted = putterXbox.DeleteOldFiles(daysToKeepTanks, "Tanks");
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
            string connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;
            DbProvider provider = new DbProvider(connectionString);
            ClanPlataform[] clans = provider.GetClans().ToArray();
            Log.InfoFormat("{0} clas devem ser calculados.", clans.Length);

            DbRecorder recorder = new DbRecorder(connectionString);

            FtpPutter putterXbox = new FtpPutter(ConfigurationManager.AppSettings["FtpFolder"],
                ConfigurationManager.AppSettings["FtpUser"],
                ConfigurationManager.AppSettings["FtpPassworld"]);

            FtpPutter putterPs = new FtpPutter(ConfigurationManager.AppSettings["PsFtpFolder"],
                ConfigurationManager.AppSettings["PsFtpUser"],
                ConfigurationManager.AppSettings["PsFtpPassworld"]);

            string resultDirectory = ConfigurationManager.AppSettings["ResultDirectory"];
            string resultDirectoryPs = ConfigurationManager.AppSettings["PsResultDirectory"];


            HashSet<string> already = new HashSet<string>(File.ReadAllLines(Path.Combine(resultDirectory, "CalcTask.txt")));
            HashSet<string> alreadyPs = new HashSet<string>(File.ReadAllLines(Path.Combine(resultDirectoryPs, "CalcTask.txt")));

            object o = new object();

            // Calcula cada cla
            int doneCount = 0;
            Stopwatch sw = Stopwatch.StartNew();
            Parallel.For(0, clans.Length, new ParallelOptions { MaxDegreeOfParallelism = 2 }, i =>
              {
                  ClanPlataform clan = clans[i];

                  bool done = false;
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
                  Stopwatch csw = Stopwatch.StartNew();

                  Clan cc = CalculateClan(clan, provider, recorder);

                  Log.InfoFormat("Calculado cla {0} de {1}: {2}@{3} em {4:N1}s...",
                      i + 1, clans.Length, clan.ClanTag, clan.Plataform, csw.Elapsed.TotalSeconds);

                  if (cc != null)
                  {
                      Stopwatch fsw = Stopwatch.StartNew();
                      switch (cc.Plataform)
                      {
                          case Platform.XBOX:
                              {
                                  string fileName = cc.ToFile(resultDirectory);
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
                                  string fileName = cc.ToFile(resultDirectoryPs);
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
            TimeSpan calculationTime = sw.Elapsed;
        }

        private static Clan CalculateClan(ClanPlataform clan, DbProvider provider,
            DbRecorder recorder)
        {
            Log.DebugFormat("Calculando cla {0}@{1}...", clan.ClanTag, clan.Plataform);

            Clan cc = provider.GetClan(clan.Plataform, clan.ClanId);

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
            TimeSpan webCacheAge = TimeSpan.FromMinutes(10);
            string cacheDirectory = ConfigurationManager.AppSettings["CacheDirectory"];
            Fetcher fetcher = new Fetcher(cacheDirectory)
            {
                WebCacheAge = webCacheAge,
                WebFetchInterval = TimeSpan.FromSeconds(1),
                ApplicationId = ConfigurationManager.AppSettings["WgApi"]
            };
            Negri.Wot.Diagnostics.SiteDiagnostic si = fetcher.GetSiteDiagnostic("https://wotclans.com.br/api/status", ConfigurationManager.AppSettings["ApiAdminKey"]);
        }

        #region Valores Esperados de WN8

        private static void GetXvmWn8()
        {
            string connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;
            DbProvider provider = new DbProvider(connectionString);
            DbRecorder recorder = new DbRecorder(connectionString);

            TimeSpan webCacheAge = TimeSpan.FromMinutes(10);
            string cacheDirectory = ConfigurationManager.AppSettings["CacheDirectory"];

            Fetcher fetcher = new Fetcher(cacheDirectory)
            {
                WebCacheAge = webCacheAge,
                WebFetchInterval = TimeSpan.FromSeconds(1),
                ApplicationId = ConfigurationManager.AppSettings["WgApi"]
            };

            Negri.Wot.Tanks.Tank[] data = provider.EnumTanks(Platform.XBOX).ToArray();
        }

        #endregion

        #region Dados de WoTStatConsole

        private static void GetFromWoTStatConsole()
        {
            string connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;
            DbProvider provider = new DbProvider(connectionString);
            DbRecorder recorder = new DbRecorder(connectionString);

            TimeSpan webCacheAge = TimeSpan.FromMinutes(10);
            string cacheDirectory = ConfigurationManager.AppSettings["CacheDirectory"];

            Fetcher fetcher = new Fetcher(cacheDirectory)
            {
                WebCacheAge = webCacheAge,
                WebFetchInterval = TimeSpan.FromSeconds(1),
                ApplicationId = ConfigurationManager.AppSettings["WgApi"]
            };

            Log.Debug("Obtendo jogadores a atualizar.");
            const int ageHours = 24;
            Player[] players = provider.GetPlayersUpdateOrder(1000, ageHours).Where(p => p.AdjustedAgeHours < 1000).Take(1000).ToArray();

            if (players.Length <= 0)
            {
                Log.Warn("Fila vazia!");
                return;
            }

            Log.Info($"{players.Length} na fila.");

            Stopwatch sw = Stopwatch.StartNew();

            CancellationTokenSource cts = new CancellationTokenSource();
            ParallelOptions po = new ParallelOptions
            {
                CancellationToken = cts.Token,
                MaxDegreeOfParallelism = 2
            };

            bool isEnd = false;
            int count = 0;
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

                Player player = players[i];
                Task<Player> task = fetcher.GetPlayerWn8Async(player);
                task.Wait(po.CancellationToken);

                Player completePlayer = task.Result;
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
            string connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;
            DbProvider provider = new DbProvider(connectionString);


            TankReference[] references = provider.GetTanksReferences(Platform.PS, new DateTime(2018, 03, 12))
                .ToArray();
            string baseDir = ConfigurationManager.AppSettings["PsResultDirectory"];
            string dir = Path.Combine(baseDir, "Tanks");

            List<Leader> leaders = new List<Leader>();
            foreach (TankReference r in references)
            {
                r.Save(dir);
                Log.InfoFormat("Escrito {0}", r.Name);
                leaders.AddRange(r.Leaders);
            }

            string json = JsonConvert.SerializeObject(leaders, Formatting.Indented);
            string file = Path.Combine(dir, $"{references.First().Date:yyyy-MM-dd}.Leaders.json");
            File.WriteAllText(file, json, Encoding.UTF8);

            StringBuilder sb = new StringBuilder();
            foreach (Leader leader in leaders)
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
            string connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;
            DbProvider provider = new DbProvider(connectionString);

            const Platform plataform = Platform.XBOX;

            DateTime date = new DateTime(2017, 03, 10);
            DateTime maxDate = new DateTime(2017, 04, 26);

            while (date <= maxDate)
            {
                Dictionary<long, TankMoe> moes = provider.GetMoe(plataform, date).ToDictionary(t => t.TankId);
                DateTime dateOnDb = moes.First().Value.Date;

                string json = JsonConvert.SerializeObject(moes, Formatting.Indented);

                string baseDir = ConfigurationManager.AppSettings["ResultsFolder"];
                string file = Path.Combine(baseDir, "MoE", $"{dateOnDb:yyyy-MM-dd}.moe.json");
                File.WriteAllText(file, json, Encoding.UTF8);

                date = date.AddDays(1.0);

                Log.InfoFormat("Escrito {0}", file);
            }
        }

        #endregion

        #region Pegar dados de Tanques

        private static void GetAllTanks(string[] args)
        {
            Fetcher fetcher = new Fetcher(ConfigurationManager.AppSettings["CacheDirectory"])
            {
                WebFetchInterval = TimeSpan.FromSeconds(5),
                ApplicationId = ConfigurationManager.AppSettings["WgApi"]
            };

            int topCount = 20;
            if (args.Length >= 1)
            {
                topCount = int.Parse(args[0]);
            }

            Log.InfoFormat("Top Count: {0}", topCount);

            string dir = "c:\\Projects\\wotclans\\TopTanks";
            if (args.Length >= 2)
            {
                dir = args[2];
            }

            Log.InfoFormat("Directory: {0}", dir);

            // Obtem todos os tanques do jogo       
            Log.Debug("Obtendo todos os tanques do jogo...");
            Dictionary<long, Tank> allTanks = fetcher.GetTanks(Platform.XBOX).ToDictionary(t => t.TankId);
            StringBuilder sb = new StringBuilder();
            foreach (Tank tank in allTanks.Values)
            {
                sb.Append(
                    $"{tank.TankId}\t{tank.Name}\t{tank.Images["big_icon"]}\t{tank.IsPremium}\t{tank.NationString}\t{tank.ShortName}\t{tank.Tag}\t{tank.Tier}\t{tank.TypeString}\r\n");
            }

            string tanksFile = $"{dir}\\AllTanks.{DateTime.Today:yyyy-MM-dd}.txt";
            File.WriteAllText(tanksFile, sb.ToString(), Encoding.UTF8);
            Log.InfoFormat("Salvos {0} tanques em {1}", allTanks.Count, tanksFile);

            string connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;
            DbProvider provider = new DbProvider(connectionString);

            // Lista todos os clas XBOX
            Clan[] allClans =
                provider.GetClans().Where(c => c.Plataform == Platform.XBOX).Select(cp => provider.GetClan(cp))
                    .OrderByDescending(c => c.Top15Wn8).ToArray();
            Log.InfoFormat("Obtidos {0} clas.", allClans.Length);

            // Seleciona a amostragem
            Clan[] topClans = allClans.Take(topCount).ToArray();
            int topPlayersCount = topClans.Sum(c => c.Active);

            int i = allClans.Length - 1;
            List<Clan> bottonClans = new List<Clan>();
            while (bottonClans.Sum(c => c.Active) < topPlayersCount && i >= 0)
            {
                bottonClans.Add(allClans[i--]);
            }


            string[] clanTags = new[]
            {
                "ETBR", "UNOT", "BOPBR", "BK", "FERAS", "BR", "VIS", "171BS", "GDAB3", "TCF",
                "DV", "TOPBR", "OWS", "AR-15", "DBD", "NT", "ITA", "13RCM", "BOPE", "-RSA-"
            };
            foreach (Clan clanTag in topClans)
            {
                Console.WriteLine(@"cla {0}...", clanTag.Name);
                File.Delete($"{dir}\\AllStats.{clanTag}.{DateTime.Today:yyyy-MM-dd}.txt");

                Clan cc = clanTag;

                foreach (Player player in cc.Players)
                {
                    sb = new StringBuilder();


                    Console.WriteLine(@"    Jogador {0}.{1}@{2}...", player.Id, player.Name, clanTag);

                    Task<IEnumerable<Negri.Wot.WgApi.TankPlayer>> tanksTask = fetcher.GetTanksForPlayerAsync(Platform.XBOX, player.Id);
                    tanksTask.Wait();
                    Negri.Wot.WgApi.TankPlayer[] tanks = tanksTask.Result.ToArray();
                    foreach (Negri.Wot.WgApi.TankPlayer t in tanks)
                    {
                        if (!allTanks.TryGetValue(t.TankId, out Tank td))
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
            Fetcher fetcher = new Fetcher(@"C:\Projects\wotclans\Cache")
            {
                WebFetchInterval = TimeSpan.FromSeconds(1),
                ApplicationId = ConfigurationManager.AppSettings["WgApi"]
            };
            Clan[] clans = fetcher.GetClans(Platform.XBOX, size).ToArray();

            DbProvider provider = new DbProvider(ConfigurationManager.ConnectionStrings["Main"].ConnectionString);

            int newClans = 0;
            foreach (Clan clan in clans)
            {
                Console.Write(@"{0} ({1}). Já existe? ", clan.ClanTag, clan.ClanId);
                Clan existingClan = provider.GetClan(clan);
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
            FtpPutter putter = new FtpPutter(ConfigurationManager.AppSettings["FtpFolder"],
                ConfigurationManager.AppSettings["FtpUser"],
                ConfigurationManager.AppSettings["FtpPassworld"]);
            IEnumerable<string> remoteClanFiles = putter.List("clan.TERSP.");
            foreach (string remoteClanFile in remoteClanFiles)
            {
                putter.DeleteFile(remoteClanFile);
            }
        }

        /// <summary>
        ///     Obtem todos os clas do jogo
        /// </summary>
        private static void GetAllClans()
        {
            List<string> ids = new List<string>();

            using (HttpClient client = new HttpClient())
            {
                // Numero de Páginas a serem lidas
                int lastSize = 100;
                for (int i = 1; i <= 62 && lastSize >= 7; ++i)
                {
                    Console.WriteLine("Consultando página {0}...", i);

                    string urlRequest =
                        $"https://api-xbox-console.worldoftanks.com/wotx/clans/list/?application_id=demo&fields=clan_id%2Ctag%2Cname%2Cmembers_count&page_no={i}";
                    HttpResponseMessage result = client.GetAsync(urlRequest).Result;
                    string json = result.Content.ReadAsStringAsync().Result;
                    ClanInfoResult clanInfoResult = JsonConvert.DeserializeObject<ClanInfoResult>(json);
                    if (clanInfoResult.Status == "ok" && clanInfoResult.Meta.Count >= 1)
                    {
                        foreach (ClanInfo c in clanInfoResult.Data)
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
            string[] clansTags = File.ReadAllLines(clansToIdFile, Encoding.UTF8).Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
            List<string> ids = new List<string>();

            using (HttpClient client = new HttpClient())
            {
                foreach (string tag in clansTags)
                {
                    string urlRequest =
                        $"https://api-xbox-console.worldoftanks.com/wotx/clans/list/?application_id=demo&search={tag}&limit=1&fields=clan_id%2Ctag";
                    HttpResponseMessage result = client.GetAsync(urlRequest).Result;
                    string json = result.Content.ReadAsStringAsync().Result;
                    ClanInfoResult clanInfoResult = JsonConvert.DeserializeObject<ClanInfoResult>(json);
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