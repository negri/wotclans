using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using log4net;
using Negri.Wot.Sql;
using Negri.Wot.Tanks;

namespace Negri.Wot.Commands
{
    [Command("CalculateStats", Description = "Calculate statistics for tanks and players")]
    public class CalculateStats : ICommand
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CalculateStats));

        private readonly Fetcher _fetcher;
        private readonly Putter _putter;
        private readonly DbProvider _provider;
        private readonly DbRecorder _recorder;
        
        public CalculateStats(Fetcher fetcher, Putter putter, DbProvider provider, DbRecorder recorder)
        {
            _fetcher = fetcher;
            _provider = provider;
            _recorder = recorder;
            _putter = putter;
        }

        /// <summary>
        /// When to "Change de Day" on the database. The default -7 corresponds roughly for when WG resets their servers.
        /// </summary>
        [CommandOption("UtcShiftToCalculate", Description = "When to \"Change de Day\" on the database.")]
        public int UtcShiftToCalculate { get; set; } = -7;

        [CommandOption("MaxParallel", Description = "Maximum parallel threads.")]
        public int MaxParallel { get; set; } = 4;

        [CommandOption("TopLeaders", Description = "The number of leaders for each tank.")]
        public int TopLeaders { get; set; } = 50;

        public ValueTask ExecuteAsync(IConsole console)
        {
            Log.Info($"Starting {nameof(CalculateStats)}...");

            var csw = Stopwatch.StartNew();
            _recorder.CalculateReference(UtcShiftToCalculate);
            csw.Stop();
            Log.Debug($"Statistics for tanks calculated in {csw.Elapsed.TotalSeconds:N0}s.");

            // Get the last one to see of there's the need to upload to the remote site
            var siteDiagnostic = _fetcher.GetSiteDiagnostic();
            var lastLeaderboard = siteDiagnostic.TankLeadersLastDate;
            Log.Info($"Last leaderboard on site: {lastLeaderboard:yyyy-MM-dd}");

            var cd = DateTime.UtcNow.AddHours(UtcShiftToCalculate);
            var previousMonday = cd.PreviousDayOfWeek(DayOfWeek.Monday);
            Log.Info($"Previous Monday: {previousMonday:yyyy-MM-dd}");

            if (previousMonday <= lastLeaderboard)
            {
                Log.Info("No need to upload.");
                return default;
            }

            Log.Info($"Getting tanks stats for {previousMonday:yyyy-MM-dd}...");
            var references = _provider.GetTanksReferences(previousMonday, null, true, false, true, TopLeaders).ToArray();
            Log.Debug($"Data for {references.Length} tanks retrieved.");
            var leaders = new ConcurrentBag<Leader>();

            Parallel.For(0, references.Length, new ParallelOptions {MaxDegreeOfParallelism = MaxParallel}, i =>
            {
                var r = references[i];

                Log.Debug($"Putting references for tank {r.Name}...");
                if (!_putter.Put(r))
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
            if (!_putter.Put(previousMonday, orderedLeaders))
            {
                Log.Error("Error putting leaders to the server.");
            }

            console.Output.WriteLine("Done!");
            Log.Info($"Done {nameof(CalculateStats)}.");

            return default;
        }


    }
}