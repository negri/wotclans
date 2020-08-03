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
using Newtonsoft.Json;

namespace Negri.Wot.Commands
{
    [Command("CalculateStats", Description = "Calculate statistics for tanks and players")]
    public class CalculateStats : ICommand
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CalculateStats));

        private readonly Fetcher _fetcher;
        private readonly FtpPutter _ftpPutter;
        private readonly DbProvider _provider;
        private readonly DbRecorder _recorder;
        private readonly string _resultDirectory;

        public CalculateStats(Fetcher fetcher, FtpPutter ftpPutter, DbProvider provider, DbRecorder recorder, string resultDirectory)
        {
            _fetcher = fetcher;
            _provider = provider;
            _recorder = recorder;
            _resultDirectory = resultDirectory;
            _ftpPutter = ftpPutter;
        }

        /// <summary>
        /// When to "Change de Day" on the database. The default -7 corresponds roughly for when WG resets their servers.
        /// </summary>
        [CommandOption("UtcShiftToCalculate", Description = "When to \"Change de Day\" on the database.")]
        public int UtcShiftToCalculate { get; set; } = -7;

        [CommandOption("MaxParallel", Description = "Maximum parallel threads.")]
        public int MaxParallel { get; set; } = 4;

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
            var references = _provider.GetTanksReferences(previousMonday).ToArray();
            Log.Debug($"Data for {references.Length} tanks retrieved.");
            var referencesDir = Path.Combine(_resultDirectory, "Tanks");
            var leaders = new ConcurrentBag<Leader>();

            Parallel.For(0, references.Length, new ParallelOptions { MaxDegreeOfParallelism = MaxParallel }, i =>
            {
                var r = references[i];
                var tankFile = r.Save(referencesDir);

                try
                {
                    _ftpPutter.PutTankReference(tankFile);
                    Log.Debug($"Upload done for tank {r.Name}");
                }
                catch (Exception ex)
                {
                    Log.Error($"Error putting tank reference files for tank {r.Name}", ex);
                }

                foreach (var leader in r.Leaders)
                {
                    leaders.Add(leader);
                }
            });

            var json = JsonConvert.SerializeObject(leaders.ToArray(), Formatting.Indented);
            var leadersFile = Path.Combine(referencesDir, $"{previousMonday:yyyy-MM-dd}.Leaders.json");
            File.WriteAllText(leadersFile, json, Encoding.UTF8);

            Log.Info("Uploading leaderboard...");
            _ftpPutter.PutTankReference(leadersFile);

            console.Output.WriteLine("Done!");
            Log.Info($"Done {nameof(CalculateStats)}.");

            return default;
        }

       
    }
}