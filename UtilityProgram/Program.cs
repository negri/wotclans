using log4net;
using Negri.Wot;
using Negri.Wot.Sql;
using Negri.Wot.Tanks;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
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
                TestClanRename();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex);
                return 1;
            }

            return 0;
        }

        /// <summary>
        ///     Dispara a deleção de arquivos no servidor usando o método de API
        /// </summary>
        private static void TestClanRename()
        {
            var putter = new Putter(ConfigurationManager.AppSettings["ApiAdminKey"])
            {
                BaseUrl = "http://localhost/ClanStatsWeb"
            };
            putter.RenameClan("ZAS-K", "GO");
        }

        /// <summary>
        /// At 2020-03-05 WG, in preparation to cross-play, changed 
        /// </summary>
        private static void MapPs4NewIds(string[] args)
        {
            var inFile = args[0];
            var all = File.ReadAllLines(inFile, Encoding.UTF8);
            var clans = new List<ClanData>(all.Length);
            clans.AddRange(all.Select(ClanData.FromString));

            var cacheDirectory = ConfigurationManager.AppSettings["CacheDirectory"];
            var fetcher = new Fetcher(cacheDirectory)
            {
                WebCacheAge = TimeSpan.FromMinutes(15),
                WebFetchInterval = TimeSpan.FromSeconds(1),
                WargamingApplicationId = ConfigurationManager.AppSettings["WgApi"]
            };

            foreach (var clan in clans)
            {
                var c = fetcher.FindClan(clan.Tag);
                if (c != null)
                {
                    clan.NewClanId = c.ClanId;
                }
            }

            var sb = new StringBuilder();
            foreach (var clan in clans)
            {
                sb.AppendLine(clan.ToString());
            }

            var outFile = args[1];
            File.WriteAllText(outFile, sb.ToString(), Encoding.UTF8);
        }

        private class ClanData
        {
            public long OldClanId { get; set; }
            public string Tag { get; set; }
            public string Name { get; set; }
            public long NewClanId { get; set; } = -1;

            public static ClanData FromString(string s)
            {
                var f = s.Split('\t');

                return new ClanData
                {
                    OldClanId = long.Parse(f[0]),
                    Tag = f[1],
                    Name = f[2]
                };
            }

            public override string ToString()
            {
                return $"{OldClanId}\t{Tag}\t{Name}\t{NewClanId}";
            }
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
                WargamingApplicationId = ConfigurationManager.AppSettings["WgApi"]
            };

            var gameMedals = fetcher.GetMedals().ToDictionary(m => m.Code);
            var maxCode = gameMedals.Values.Max(m => m.Code.Length);
            var maxName = gameMedals.Values.Max(m => m.Name.Length);
            var maxDescription = gameMedals.Values.Max(m => m.Description.Length);
            var maxHeroInformation = gameMedals.Values.Max(m => m.HeroInformation?.Length ?? 0);
            var maxCondition = gameMedals.Values.Max(m => m.Condition?.Length ?? 0);

            var connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;
            var recorder = new DbRecorder(connectionString);
            recorder.Set(gameMedals.Values);

        }

        private static void TestGetClan()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;
            var provider = new DbProvider(connectionString);
            var clan = provider.GetClan(208);
        }


        private static void PutPlayer(long playerId)
        {
            var connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;
            var provider = new DbProvider(connectionString);
            var player = provider.GetPlayer(playerId, true);
            player.Calculate(provider.GetWn8ExpectedValues());

            var putter = new Putter(ConfigurationManager.AppSettings["ApiAdminKey"])
            {
                BaseUrl = "http://localhost:6094/"
            };
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



        private static void CalculateAverageWn8OfAllTanks()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;
            var provider = new DbProvider(connectionString);

            var cd = DateTime.UtcNow.AddHours(-7);
            var previousMonday = cd.PreviousDayOfWeek(DayOfWeek.Monday);

            var references = provider.GetTanksReferences(previousMonday).ToArray();

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
            var expected = provider.GetWn8ExpectedValues();

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
            var cleanerXbox = new Putter(ConfigurationManager.AppSettings["ApiAdminKey"]);
            cleanerXbox.CleanOldData();

            var cleanerPs = new Putter(ConfigurationManager.AppSettings["ApiAdminKey"]);
            cleanerPs.CleanOldData();
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

            var wn8 = provider.GetWn8ExpectedValues();
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
                  if (clan.Platform == Platform.XBOX)
                  {
                      done = already.Contains(clan.ClanTag);
                  }
                  else if (clan.Platform == Platform.PS)
                  {
                      done = alreadyPs.Contains(clan.ClanTag);
                  }

                  if (done)
                  {
                      Log.InfoFormat("cla {0} de {1}: {2}@{3} feito anteriormente.", i + 1, clans.Length, clan.ClanTag, clan.Platform);
                      Interlocked.Increment(ref doneCount);
                      return;
                  }

                  Log.InfoFormat("Processando cla {0} de {1}: {2}@{3}...", i + 1, clans.Length, clan.ClanTag,
                      clan.Platform);
                  var csw = Stopwatch.StartNew();

                  var cc = CalculateClan(clan, provider, recorder);

                  Log.InfoFormat("Calculado cla {0} de {1}: {2}@{3} em {4:N1}s...",
                      i + 1, clans.Length, clan.ClanTag, clan.Platform, csw.Elapsed.TotalSeconds);

                  if (cc != null)
                  {
                      var fsw = Stopwatch.StartNew();
                      switch (cc.Platform)
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
                          i + 1, clans.Length, clan.ClanTag, clan.Platform, fsw.Elapsed.TotalSeconds);
                  }

                  Interlocked.Increment(ref doneCount);
                  Log.InfoFormat("Processado cla {0} de {1}: {2}@{3} em {4:N1}s. {5} totais.",
                      i + 1, clans.Length, clan.ClanTag, clan.Platform, csw.Elapsed.TotalSeconds, doneCount);
              });
            var calculationTime = sw.Elapsed;
        }

        private static Clan CalculateClan(ClanBaseInformation clan, DbProvider provider,
            DbRecorder recorder)
        {
            Log.DebugFormat("Calculando cla {0}@{1}...", clan.ClanTag, clan.Platform);

            var cc = provider.GetClan(clan.ClanId);

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
            Log.InfoFormat("cla:                     {0}@{1}", cc.ClanTag, cc.Platform);
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
                WargamingApplicationId = ConfigurationManager.AppSettings["WgApi"],
                WotClansAdminApiKey = ConfigurationManager.AppSettings["ApiAdminKey"]
            };
            var si = fetcher.GetSiteDiagnostic();
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
                WargamingApplicationId = ConfigurationManager.AppSettings["WgApi"]
            };

            var data = provider.EnumTanks().ToArray();
        }

        #endregion

        #region Referencias

        private static void CalculateAndPutReferences()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;
            var provider = new DbProvider(connectionString);
            var recorder = new DbRecorder(connectionString);

            var cacheDirectory = ConfigurationManager.AppSettings["CacheDirectory"];
            var wargamingApplicationId = ConfigurationManager.AppSettings["WgAppId"];
            var wotClansAdminApiKey = ConfigurationManager.AppSettings["ApiAdminKey"];
            var webCacheAge = TimeSpan.FromMinutes(1);

            var fetcher = new Fetcher(cacheDirectory)
            {
                WebCacheAge = webCacheAge,
                WebFetchInterval = TimeSpan.FromSeconds(1),
                WargamingApplicationId = wargamingApplicationId,
                WotClansAdminApiKey = wotClansAdminApiKey,
                WotClansBaseUrl = "http://localhost/ClanStatsWeb"
            };

            var putter = new Putter(wotClansAdminApiKey)
            {
                BaseUrl = "http://localhost/ClanStatsWeb"
            };

            const int utcShiftToCalculate = -7;
            const int topLeaders = 50;
            const int maxParallel = 1;

            recorder.CalculateReference(utcShiftToCalculate);

            var siteDiagnostic = fetcher.GetSiteDiagnostic();
            var lastLeaderboard = siteDiagnostic.TankLeadersLastDate;
            Log.Info($"Last leaderboard on site: {lastLeaderboard:yyyy-MM-dd}");

            var cd = DateTime.UtcNow.AddHours(utcShiftToCalculate);
            var previousMonday = cd.PreviousDayOfWeek(DayOfWeek.Monday);
            Log.Info($"Previous Monday: {previousMonday:yyyy-MM-dd}");

            if (previousMonday <= lastLeaderboard)
            {
                Log.Info("No need to upload.");
                return;
            }

            Log.Info($"Getting tanks stats for {previousMonday:yyyy-MM-dd}...");
            var references = provider.GetTanksReferences(previousMonday, null, true, false, true, topLeaders).ToArray();
            Log.Debug($"Data for {references.Length} tanks retrieved.");
            var leaders = new ConcurrentBag<Leader>();

            Parallel.For(0, references.Length, new ParallelOptions { MaxDegreeOfParallelism = maxParallel }, i =>
            {
                var r = references[i];

                Log.Debug($"Putting references for tank {r.Name}...");
                if (!putter.Put(r))
                {
                    Log.Error($"Error putting tank reference files for tank {r.Name}.");
                }

                foreach (var leader in r.Leaders)
                {
                    leaders.Add(leader);
                }
            });

            var orderedLeaders = leaders.OrderByDescending(l => l.Tier).ThenBy(l => l.Type).ThenBy(l => l.Nation).ThenBy(l => l.Name).ThenBy(l => l.Order)
                .ToArray();
            Log.Info($"Uploading leaderboard with {orderedLeaders.Length} players...");
            if (!putter.Put(previousMonday, orderedLeaders))
            {
                Log.Error("Error putting leaders to the server.");
            }

        }


        #endregion

        #region MoE

        private static void DumpMoEFiles(string[] args)
        {
            var connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;
            var provider = new DbProvider(connectionString);


            var date = new DateTime(2017, 03, 10);
            var maxDate = new DateTime(2017, 04, 26);

            while (date <= maxDate)
            {
                var moes = provider.GetMoe(date).ToDictionary(t => t.TankId);
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


        #region Pegar dados de clas

        private static void ListClans(int size)
        {
            var fetcher = new Fetcher(@"C:\Projects\wotclans\Cache")
            {
                WebFetchInterval = TimeSpan.FromSeconds(1),
                WargamingApplicationId = ConfigurationManager.AppSettings["WgApi"]
            };
            var clans = fetcher.GetClans(size).ToArray();

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