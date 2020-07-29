using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using log4net;
using Negri.Wot.Sql;
using Negri.Wot.Tanks;

namespace Negri.Wot.Commands
{
    [Command("GetPlayers", Description = "Fetch player statistics.")]
    public class GetPlayers : ICommand
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(GetPlayers));

        private readonly Fetcher _fetcher;
        private readonly DbProvider _provider;
        private readonly DbRecorder _recorder;
        private readonly Putter _putter;
        
        public GetPlayers(Fetcher fetcher, Putter putter, DbProvider provider, DbRecorder recorder)
        {
            _fetcher = fetcher;
            _provider = provider;
            _recorder = recorder;
            _putter = putter;
        }

        [CommandOption("WebFetchInterval", Description = "Interval between web queries on APIs.")]
        public TimeSpan WebFetchInterval { get; set; } = TimeSpan.Zero;

        [CommandOption("WebCacheAge", Description = "Maximum age of cached web data.")]
        public TimeSpan WebCacheAge { get; set; } = TimeSpan.FromMinutes(50);

        [CommandOption("AgeHours", Description = "Hours after a player being update that new data should be fetched.")]
        public int AgeHours { get; set; } = 24;

        [CommandOption("MaxPlayers", Description = "Maximum players to retrieve per run.")]
        public int? MaxPlayers { get; set; }

        [CommandOption("MaxRunMinutes", Description = "Maximum run minutes.")]
        public int? MaxRunMinutes { get; set; }

        [CommandOption("MaxParallel", Description = "Maximum parallel threads.")]
        public int MaxParallel { get; set; } = 2;

        [CommandOption("PutPlayers", Description = "If players should be pushed to the site.")]
        public bool PutPlayers { get; set; } = true;

        [CommandOption("Kp", Description = "Kp (Error) parameter to throttle the fetch.")]
        public double Kp { get; set; } = 2.0;

        [CommandOption("Ki", Description = "Ki (Integral) parameter to throttle the fetch.")]
        public double Ki { get; set; } = 0.0000001;

        [CommandOption("Kd", Description = "Kd (Derivative) parameter to throttle the fetch.")]
        public double Kd { get; set; } = 1.0;

        public ValueTask ExecuteAsync(IConsole console)
        {
            var sw = Stopwatch.StartNew();

            console.Output.WriteLine($"Starting {nameof(GetPlayers)}...");

            Log.Info("------------------------------------------------------------------------------------");
            Log.Info("GetPlayers starting...");
            Log.Info($"Compiled at {RetrieveLinkerTimestamp():yyyy-MM-dd HH:mm}; running on directory {Environment.CurrentDirectory}");
            Log.Info($"ageHours: {AgeHours}; maxParallel: {MaxParallel}; putPlayers: {PutPlayers}");
            Log.Info($"kp: {Kp:R}; ki: {Ki:R}; kd: {Kd:R}");

            _fetcher.WebFetchInterval = WebFetchInterval;
            _fetcher.WebCacheAge = WebCacheAge;

            #region How Much Work to do

            if (MaxRunMinutes == null)
            {
                MaxRunMinutes = 60 - DateTime.Now.Minute;
                if (MaxRunMinutes < 5)
                {
                    MaxRunMinutes = 5;
                }
            }

            var dbInfo = _provider.GetDataDiagnostic();
            var originalMaxPlayers = MaxPlayers ?? 0;
            if (MaxPlayers == null)
            {
                MaxPlayers = (int?) (dbInfo.ScheduledPlayersPerHour / 60.0 * MaxRunMinutes.Value);
                originalMaxPlayers = MaxPlayers.Value;
                MaxPlayers = (int?) (MaxPlayers.Value * 1.20);
            }

            Log.Info($"maxRunMinutes: {MaxRunMinutes}; maxPlayers: {MaxPlayers}; originalMaxPlayers: {originalMaxPlayers}");

            var players = _provider.GetPlayersUpdateOrder(MaxPlayers.Value, AgeHours).ToArray();
            if (players.Length <= 0)
            {
                Log.Warn("Empty Players Queue!");
                return default;
            }

            var playersPerMinute = players.Length * 1.0 / ((double) MaxRunMinutes);
            Log.Debug($"Avg playersPerMinute: {playersPerMinute:N0}");

            var recentlyUpdatedPlayers = players.Select(p => p.AdjustedAgeHours).Where(a => a < 6000).ToArray();
            if (recentlyUpdatedPlayers.Any())
            {
                var averageAgeHours = recentlyUpdatedPlayers.Take(originalMaxPlayers).Average();
                var modeAgeHours = recentlyUpdatedPlayers
                    .Take(originalMaxPlayers).Select(a => (decimal) (Math.Floor(a * 100.0) / 100.0)).GroupBy(a => a)
                    .OrderByDescending(g => g.Count()).ThenBy(g => g.Key).Select(g => g.Key).FirstOrDefault();
                Log.Info($"Average Data Age: {averageAgeHours:N2}");
                Log.Info($"Median Data Age: {modeAgeHours:N2}");
            }

            Log.Info($"Delay últimas 48h: {dbInfo.Last48HDelay:N2}");
            if (dbInfo.Last48HDelay <= AgeHours)
            {
                // Pontual, faz apenas o planejado
                Log.Debug("Not running late on the queue.");
                players = players.Take(originalMaxPlayers).ToArray();
            }

            #endregion

            #region Ensuring Tanks and Achievements are up to date

            _recorder.Set(Platform.Console, _fetcher.GetTanks(Platform.Console));
            var allTanks = _provider.GetTanks(Platform.Console).ToDictionary(t => t.TankId);
            var wn8Expected = _provider.GetWn8ExpectedValues();
            Log.Info($"Retrieved {allTanks.Count} tanks.");
            if (allTanks.Count != wn8Expected.Count)
            {
                Log.Warn($"The expected WN8 table has {wn8Expected.Count} and there is {allTanks.Count} reported. Run the command ImportXvm.");
            }

            _recorder.Set(_fetcher.GetMedals());

            #endregion

            #region Main Loop

            var idealInterval = (MaxRunMinutes.Value * 60.0 - sw.Elapsed.TotalSeconds) / players.Length;
            var threadInterval = idealInterval * MaxParallel * 0.80;
            var lockObject = new object();

            var cts = new CancellationTokenSource();
            var po = new ParallelOptions
            {
                CancellationToken = cts.Token,
                MaxDegreeOfParallelism = MaxParallel
            };

            // The speed is controlled by a PID model. See a sample at https://www.codeproject.com/Articles/36459/PID-process-control-a-Cruise-Control-example

            var integral = 0.0;
            double previousError = 0.0, sumErrorSq = 0.0;
            var previousTime = DateTime.UtcNow;
            int count = 0, controlledLoops = 0;

            try
            {
                Parallel.For(0, players.Length, po, i =>
                {
                    if (po.CancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    if (sw.Elapsed.TotalMinutes > MaxRunMinutes)
                    {
                        cts.Cancel();
                    }

                    var player = players[i];

                    var swPlayer = Stopwatch.StartNew();
                    RetrievePlayer(player, allTanks, wn8Expected);
                    swPlayer.Stop();

                    Thread.Sleep(TimeSpan.FromSeconds(threadInterval));

                    lock (lockObject)
                    {
                        Interlocked.Increment(ref count);
                        var actualInterval = sw.Elapsed.TotalSeconds / count;

                        var error = idealInterval - actualInterval;
                        var time = DateTime.UtcNow;
                        var dt = (time - previousTime).TotalSeconds;

                        Debug.Assert(MaxRunMinutes != null, nameof(MaxRunMinutes) + " != null");

                        var remainingSeconds = MaxRunMinutes.Value * 60.0 - sw.Elapsed.TotalSeconds;
                        var remainingPlayers = players.Length - count;

                        if ((count > 20) && (remainingSeconds > 60) && (remainingPlayers > 5) && (dt > 0.01))
                        {
                            integral += error * dt;
                            var derivative = (error - previousError) / dt;
                            var output = (error * Kp) + (integral * Ki) + (derivative * Kd);
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

                            Log.Info($"i: {i:0000}; {player.Id:00000000000}@{player.Platform.ToString().PadRight(4)}; Count: {count:0000}; " +
                                     $"TI: {threadInterval:00.00}; Actual: {actualInterval:00.00}; Ideal: {idealInterval:00.00}; " +
                                     $"out: {output:+00.000;-00.000}; dt: {dt:000.000}; rt: {swPlayer.Elapsed.TotalSeconds:000.000}; Target: {MaxRunMinutes * 60.0 / actualInterval:0000}; ");

                            idealInterval = remainingSeconds / remainingPlayers;
                        }
                        else
                        {
                            Log.Info($"i: {i:0000}; {player.Id:00000000000}@{player.Platform.ToString().PadRight(4)}; Count: {count:0000}; " +
                                     $"TI: {threadInterval:00.00}; Actual: {actualInterval:00.00}; Ideal: {idealInterval:00.00}; " +
                                     $"out: {0.0:+00.000;-00.000}; dt: {dt:000.000}; rt: {swPlayer.Elapsed.TotalSeconds:000.000}; Target: {MaxRunMinutes * 60.0 / actualInterval:0000}; ");
                        }

                        previousError = error;
                        previousTime = time;
                    }
                });
            }
            catch (OperationCanceledException)
            {
                Log.WarnFormat("Tempo esgotado antes de concluir a fila no {0} de {1}. Erro de Controle: {2:N4}", count, players.Length,
                    sumErrorSq / controlledLoops);

                Log.Info("Waiting a minute to clean up upload tasks...");
                Thread.Sleep(TimeSpan.FromMinutes(1));

                Log.Debug("Execution complete.");
                console.Output.WriteLine("Done!");

                return default;
            }

            #endregion

            Log.Info($"Finishing in {sw.Elapsed}. Done {players.Length}. Control Error: {(sumErrorSq / controlledLoops):N4}");

            Log.Info("Waiting a minute to clean up upload tasks...");
            Thread.Sleep(TimeSpan.FromMinutes(1));

            Log.Debug("Execution complete.");
            console.Output.WriteLine("Done!");
            return default;
        }


        private void RetrievePlayer(Player player, IReadOnlyDictionary<long, Tank> allTanks,
            Wn8ExpectedValues wn8Expected)
        {
            var tanks = _fetcher.GetTanksForPlayer(player.Id);
            var validTanks = tanks.Where(t => allTanks.ContainsKey(t.TankId)).ToArray();
            _recorder.Set(validTanks);

            var played = _provider.GetWn8RawStatsForPlayer(player.Id);
            player.Performance = played;
            player.Calculate(wn8Expected);
            player.Moment = DateTime.UtcNow;
            player.Origin = PlayerDataOrigin.Self;

            var previous = _provider.GetPlayer(player.Id, player.Date, true);
            if (previous != null)
            {
                if (player.Check(previous, true))
                {
                    Log.Warn($"Player {player.Name}.{player.Id}@{player.Platform} was patched.");
                }
            }

            if (player.CanSave())
            {
                _recorder.Set(player);
                if (!player.IsPatched && PutPlayers)
                {
                    _ = Task.Run(() =>
                    {
                        try
                        {
                            _putter?.Put(player);
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"Error putting player {player.Id} on the remote site.", ex);
                        }
                    });
                }
            }
            else
            {
                Log.Warn($"Player {player.Name}.{player.Id}@{player.Platform} has to much zero data.");
            }
        }


        /// <summary>
        ///     Retrieves the linker timestamp.
        /// </summary>
        /// <remarks>
        ///     http://stackoverflow.com/questions/1600962/displaying-the-build-date
        /// </remarks>
        private static DateTime RetrieveLinkerTimestamp()
        {
            var filePath = Assembly.GetCallingAssembly().Location;
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

            var i = BitConverter.ToInt32(b, peHeaderOffset);
            var secondsSince1970 = BitConverter.ToInt32(b, i + linkerTimestampOffset);
            var dt = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            dt = dt.AddSeconds(secondsSince1970);
            dt = dt.ToLocalTime();
            return dt;
        }
    }
}