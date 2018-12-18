using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using log4net;
using Negri.Wot.Sql;
using Negri.Wot.Tanks;
using System.Threading.Tasks;

namespace Negri.Wot
{
    internal class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Program));

        private static int Main(string[] args)
        {
            try
            {
                var sw = Stopwatch.StartNew();

                ParseParans(args, out var ageHours, out var maxPlayers, out var maxRunMinutes,
                    out var webCacheAge, out var kp, out var ki, out var kd, out var maxParallel, out var queryWotStatConsoleApi, 
                    out var putPlayers);

                var cacheDirectory = ConfigurationManager.AppSettings["CacheDirectory"];

                Log.Info("------------------------------------------------------------------------------------");
                Log.Info("FetchPlayers iniciando...");
                Log.Info($"Compilado em {RetrieveLinkerTimestamp():yyyy-MM-dd HH:mm}; executando no diretorio {Environment.CurrentDirectory}");
                Log.InfoFormat("ageHours: {0}; cacheDirectory: {1}, maxParallel: {2}, queryWotStatConsoleApi: {3}", ageHours, cacheDirectory, maxParallel, queryWotStatConsoleApi);
                Log.Info($"kp: {kp:R}; ki: {ki:R}; kd: {kd:R}");
                
                var connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;
                var provider = new DbProvider(connectionString);
                var recorder = new DbRecorder(connectionString);

                if (maxRunMinutes == null)
                {
                    maxRunMinutes = 60 - DateTime.Now.Minute;
                    if (maxRunMinutes < 5)
                    {
                        maxRunMinutes = 5;
                    }
                }

                var dbInfo = provider.GetDataDiagnostic();
                int originalMaxPlayers = maxPlayers ?? 0;
                if (maxPlayers == null)
                {                    
                    maxPlayers = (int?) (dbInfo.ScheduledPlayersPerHour/60.0*maxRunMinutes.Value);
                    originalMaxPlayers = maxPlayers.Value;
                    maxPlayers = (int?)(maxPlayers.Value*1.20);
                }                

                Log.Info($"maxRunMinutes: {maxRunMinutes}; maxPlayers: {maxPlayers}; originalMaxPlayers: {originalMaxPlayers}");

                var players = provider.GetPlayersUpdateOrder(maxPlayers.Value, ageHours).ToArray();

                if (players.Length <= 0)
                {
                    Log.Warn("Fila vazia!");
                    return 3;
                }

                double playersPerMinute = players.Length * 1.0 / ((double) maxRunMinutes);
                Log.Debug("Avg playersPerMinute: {playersPerMinute:N0}");
                double externalApiCallFactor = 0.0;
                if (queryWotStatConsoleApi > 0)
                {
                    externalApiCallFactor = queryWotStatConsoleApi / playersPerMinute;
                    if (externalApiCallFactor > 1.0)
                    {
                        externalApiCallFactor = 1.0;
                    }
                    Log.Debug("externalApiCallFactor: {externalApiCallFactor}");
                }
                var externalCallRandom = new Random(69);

                var recentlyUpdatedPlayers = players.Select(p => p.AdjustedAgeHours).Where(a => a < 6000).ToArray();
                if (recentlyUpdatedPlayers.Any())
                {
                    var averageAgeHours = recentlyUpdatedPlayers.Take(originalMaxPlayers).Average();
                    var modeAgeHours = recentlyUpdatedPlayers
                        .Take(originalMaxPlayers).Select(a => (decimal)(Math.Floor(a * 100.0) / 100.0)).GroupBy(a => a)
                        .OrderByDescending(g => g.Count()).ThenBy(g => g.Key).Select(g => g.Key).FirstOrDefault();
                    Log.Info($"Idade média dos dados: {averageAgeHours:N2}");
                    Log.Info($"Idade moda  dos dados: {modeAgeHours:N2}");
                }                
                
                Log.Info($"Delay últimas 48h: {dbInfo.Last48HDelay:N2}");
                if (dbInfo.Last48HDelay <= ageHours)
                {
                    // Pontual, faz apenas o planejado
                    Log.InfoFormat("Não está atrasado.");
                    players = players.Take(originalMaxPlayers).ToArray();                    
                }
                
                var fetchers = new ConcurrentQueue<Fetcher>();

                for (int i = 0; i < maxParallel * 8; ++i)
                {
                    var fetcher = new Fetcher(cacheDirectory)
                    {
                        WebCacheAge = webCacheAge,
                        WebFetchInterval = TimeSpan.FromSeconds(1),
                        ApplicationId = ConfigurationManager.AppSettings["WgApi"]
                    };
                    fetchers.Enqueue(fetcher);
                }                

                Log.Debug("Obtendo todos os tanques em XBOX...");
                var f = fetchers.Dequeue();
                recorder.Set(f.GetTanks(Plataform.XBOX));
                var allTanksXbox = provider.GetTanks(Plataform.XBOX).ToDictionary(t => t.TankId);
                fetchers.Enqueue(f);
                Log.InfoFormat("Obtidos {0} tanques para XBOX.", allTanksXbox.Count);

                Log.Debug("Obtendo todos os tanques em PS...");
                f = fetchers.Dequeue();
                recorder.Set(f.GetTanks(Plataform.PS));
                var allTanksPs = provider.GetTanks(Plataform.PS).ToDictionary(t => t.TankId);
                fetchers.Enqueue(f);
                Log.InfoFormat("Obtidos {0} tanques para PS.", allTanksPs.Count);

                // Ambas as plataformas usam os mesmos valores de referência
                var wn8Expected = provider.GetWn8ExpectedValues(Plataform.XBOX);
                
                var idealInterval = (maxRunMinutes.Value * 60.0 - sw.Elapsed.TotalSeconds) / players.Length;
                double threadInterval = idealInterval*maxParallel*0.80;
                var lockObject = new object();

                // To save on players on the remote server (Same remote DB, so the plataform doesn't matter)
                var putter = putPlayers ? new Putter(Plataform.PS, ConfigurationManager.AppSettings["ApiAdminKey"]) : null;

                var cts = new CancellationTokenSource();
                var po = new ParallelOptions
                {
                    CancellationToken = cts.Token,
                    MaxDegreeOfParallelism = maxParallel
                };

                // A velocidade do loop é controlada por um sistema PID. Veja exemplo em https://www.codeproject.com/Articles/36459/PID-process-control-a-Cruise-Control-example

                double integral = 0.0;
                double previousError = 0.0, sumErrorSq = 0.0;
                DateTime previousTime = DateTime.UtcNow;                
                int count = 0, controlledLoops = 0;

                try
                {
                    Parallel.For(0, players.Length, po, i =>
                    {
                        if (po.CancellationToken.IsCancellationRequested)
                        {
                            return;
                        }

                        if (sw.Elapsed.TotalMinutes > maxRunMinutes)
                        {
                            cts.Cancel();
                        }

                        var wg = fetchers.Dequeue();

                        Fetcher ext = null;
                        if (externalCallRandom.NextDouble() < externalApiCallFactor)
                        {
                            ext = fetchers.Dequeue();
                        }

                        var player = players[i];

                        var swPlayer = Stopwatch.StartNew();
                        var allTanks = player.Plataform == Plataform.XBOX ? allTanksXbox : allTanksPs;
                        RetrievePlayer(player, wg, ext, provider, recorder, allTanks, wn8Expected, putter);
                        swPlayer.Stop();

                        if (ext != null)
                        {
                            fetchers.Enqueue(ext);
                        }
                        
                        fetchers.Enqueue(wg);        
                        
                        Thread.Sleep(TimeSpan.FromSeconds(threadInterval));

                        lock (lockObject)
                        {
                            Interlocked.Increment(ref count);
                            var actualInterval = sw.Elapsed.TotalSeconds / count;

                            var error = idealInterval - actualInterval;
                            var time = DateTime.UtcNow;
                            var dt = (time - previousTime).TotalSeconds;

                            var remainingSeconds = maxRunMinutes.Value * 60.0 - sw.Elapsed.TotalSeconds;
                            var remainingPlayers = players.Length - count;

                            if ((count > 20) && (remainingSeconds > 60) && (remainingPlayers > 5) && (dt > 0.01))
                            {
                                integral += error * dt;
                                var derivative = (error - previousError) / dt;
                                var output = (error * kp) + (integral * ki) + (derivative * kd);
                                threadInterval += output;

                                controlledLoops++;
                                sumErrorSq += error * error;

                                if (threadInterval < 0.1)
                                {
                                    threadInterval = 0.1;
                                }
                                else if (threadInterval > 30)
                                {
                                    threadInterval = 30;
                                }

                                Log.Info($"i: {i:0000}; {player.Id:00000000000}@{player.Plataform.ToString().PadRight(4)}; Count: {count:0000}; " +
                                         $"TI: {threadInterval:00.00}; Actual: {actualInterval:00.00}; Ideal: {idealInterval:00.00}; " +
                                         $"out: {output:+00.000;-00.000}; dt: {dt:000.000}; rt: {swPlayer.Elapsed.TotalSeconds:000.000}; Target: {maxRunMinutes*60.0/actualInterval:0000}; " +
                                         $"HadExt: {ext != null}");

                                idealInterval = remainingSeconds / remainingPlayers;
                            }
                            else
                            {
                                Log.Info($"i: {i:0000}; {player.Id:00000000000}@{player.Plataform.ToString().PadRight(4)}; Count: {count:0000}; " +
                                         $"TI: {threadInterval:00.00}; Actual: {actualInterval:00.00}; Ideal: {idealInterval:00.00}; " +
                                         $"out: {0.0:+00.000;-00.000}; dt: {dt:000.000}; rt: {swPlayer.Elapsed.TotalSeconds:000.000}; Target: {maxRunMinutes * 60.0 / actualInterval:0000}; " +
                                         $"HadExt: {ext != null}");
                            }
                            
                            previousError = error;
                            previousTime = time;
                        }
                    });
                }
                catch (OperationCanceledException)
                {
                    Log.WarnFormat("Tempo esgotado antes de concluir a fila no {0} de {1}. Erro de Controle: {2:N4}",
                        count, players.Length, sumErrorSq / controlledLoops);
                    return 2;
                }

                Log.InfoFormat("FetchPlayers terminando normalmente em {0}. Feitos {1}. Erro de Controle: {2:N4}", 
                    sw.Elapsed, players.Length, sumErrorSq/controlledLoops);
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex);
                Console.WriteLine(ex);
                return 1;
            }
        }
        

        private static void RetrievePlayer(Player player, Fetcher fetcher, Fetcher externalFetcher,
            DbProvider provider, DbRecorder recorder, IReadOnlyDictionary<long, Tank> allTanks, Wn8ExpectedValues wn8Expected, Putter putter)
        {
            Task<Player> externalTask = null;
            if (externalFetcher != null)
            {
                externalTask = externalFetcher.GetPlayerWn8Async((Player)player.Clone());
            }            

            var tanks = fetcher.GetTanksForPlayer(player.Plataform, player.Id);
            var validTanks = tanks.Where(t => allTanks.ContainsKey(t.TankId)).ToArray();
            recorder.Set(validTanks);
            
            var played = provider.GetWn8RawStatsForPlayer(player.Plataform, player.Id);
            player.Performance = played;
            player.Calculate(wn8Expected);
            player.Moment = DateTime.UtcNow;
            player.Origin = PlayerDataOrigin.Self;
            
            var previous = provider.GetPlayer(player.Id, player.Date, true);
            if (previous != null)
            {
                if (player.Check(previous, true))
                {
                    Log.Warn($"Player {player.Name}.{player.Id}@{player.Plataform} was patched.");
                }
            }
            
            if (player.CanSave())
            {
                recorder.Set(player);
                if (!player.IsPatched)
                {
                    putter?.Put(player);
                }
            }
            else
            {
                Log.Warn($"Player {player.Name}.{player.Id}@{player.Plataform} has to much zero data.");
            }
            
            // Se foi feita a chamada na API externa, espero o retorno, que será ignorado; mas serviu para popular o BD do parceiro.
            externalTask?.Wait();
        }
        
        private static void ParseParans(IEnumerable<string> args, out int ageHours, out int? maxPlayers,
            out int? maxRunMinutes, out TimeSpan webCacheAge,
            out double kp, out double ki, out double kd, out int maxParallel, out int queryWotStatConsoleApi, 
            out bool putPlayers)
        {
            ageHours = 24;
            maxPlayers = null;
            maxRunMinutes = null;
            maxParallel = 2;
            queryWotStatConsoleApi = -1; // Não faz queries na API de WoTStatConsole.de
            putPlayers = true;
            
            webCacheAge = TimeSpan.FromMinutes(10);

            kp = 2.0;
            ki = 0.0000001;
            kd = 1.0;

            foreach (var arg in args)
            {
                if (arg.StartsWith("AgeHours:"))
                {
                    ageHours = int.Parse(arg.Substring("AgeHours:".Length));
                }
                else if (arg.StartsWith("MaxPlayers:"))
                {
                    ageHours = int.Parse(arg.Substring("MaxPlayers:".Length));
                }
                else if (arg.StartsWith("MaxRunMinutes:"))
                {
                    maxRunMinutes = int.Parse(arg.Substring("MaxRunMinutes:".Length));
                }
                else if (arg.StartsWith("WebCacheAgeMinutes:"))
                {
                    webCacheAge = TimeSpan.FromMinutes(int.Parse(arg.Substring("WebCacheAgeMinutes:".Length)));
                }
                else if (arg.StartsWith("kp:"))
                {
                    kp = double.Parse(arg.Substring("kp:".Length));
                }
                else if (arg.StartsWith("ki:"))
                {
                    ki = double.Parse(arg.Substring("ki:".Length));
                }
                else if (arg.StartsWith("kd:"))
                {
                    kd = double.Parse(arg.Substring("kd:".Length));
                }
                else if (arg.StartsWith("QueryWotStatConsoleApi:"))
                {
                    queryWotStatConsoleApi = int.Parse(arg.Substring("QueryWotStatConsoleApi:".Length));
                }
                else if (arg.StartsWith("PutPlayers:"))
                {
                    putPlayers = bool.Parse(arg.Substring("PutPlayers:".Length));
                }
                else if (arg.StartsWith("MaxParallel:"))
                {
                    maxParallel = int.Parse(arg.Substring("MaxParallel:".Length));
                    if (maxParallel <= 0)
                    {
                        maxParallel = 1;
                    }
                    else if (maxParallel > 8)
                    {
                        maxParallel = 8;
                    }
                }
            }
        }

        /// <summary>
        /// Retrieves the linker timestamp.
        /// </summary>
        /// <remarks>
        /// http://stackoverflow.com/questions/1600962/displaying-the-build-date
        /// </remarks>
        private static DateTime RetrieveLinkerTimestamp()
        {
            string filePath = Assembly.GetCallingAssembly().Location;
            const int peHeaderOffset = 60;
            const int linkerTimestampOffset = 8;
            var b = new byte[2048];
            Stream s = null;

            try
            {
                s = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                s.Read(b, 0, 2048);
            }
            finally
            {
                s?.Close();
            }

            int i = BitConverter.ToInt32(b, peHeaderOffset);
            int secondsSince1970 = BitConverter.ToInt32(b, i + linkerTimestampOffset);
            var dt = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            dt = dt.AddSeconds(secondsSince1970);
            dt = dt.ToLocalTime();
            return dt;
        }
    }
}