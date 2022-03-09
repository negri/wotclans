using System;
using System.Linq;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using log4net;
using Negri.Wot.Sql;

namespace Negri.Wot.Commands
{

    
    [Command("ImportWn8WotcStat", Description = "Import Expected WN8 values from WotcStat")]
    public class ImportWn8WotcStat : ICommand
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ImportWn8WotcStat));

        private readonly Fetcher _fetcher;
        private readonly Putter _putter;
        private readonly DbProvider _provider;
        private readonly DbRecorder _recorder;

        public ImportWn8WotcStat(Fetcher fetcher, Putter putter, DbProvider provider, DbRecorder recorder)
        {
            _fetcher = fetcher;
            _provider = provider;
            _recorder = recorder;
            _putter = putter;
        }

        [CommandOption("WebCacheAge", Description = "Maximum age for data retrieved from the web")]
        public TimeSpan WebCacheAge { get; set; } = TimeSpan.FromHours(1);

        [CommandOption("Compute", Description = "If the reference values for this site should be computed from the acquired data")]
        public bool Compute { get; set; } = false;

        [CommandOption("PutOnSite", Description = "If the reference values for this site should be put on the site")]
        public bool PutOnSite { get; set; } = false;

        public ValueTask ExecuteAsync(IConsole console)
        {
            Log.Info($"Starting {nameof(ImportWn8WotcStat)}...");

            _fetcher.WebCacheAge = WebCacheAge;

            // Get Tanks on the API
            var apiTanks = _fetcher.GetTanks(Platform.Console).ToArray();
            Log.Info($"{apiTanks.Length} Console Tanks retrieved from the API.");

            // Save on Database
            _recorder.Set(Platform.Console, apiTanks);
            Log.Info("Console Tanks saved.");

            // Get Expected Values
            var expected = _fetcher.GetWotcStatWn8ExpectedValues();
            Log.Info("Expected values retrieved from WotcStat");

            // WotcStat may be returning some strange ids.., most likely it's my tanks database that's outdated (WG API may lag) or it's a Cold War tank
            var tanks = apiTanks.Select(t => t.TankId).ToHashSet();
            foreach (var t in expected.AllTanks)
            {
                if (!tanks.Contains(t.TankId))
                {
                    Log.Warn($"WotcStat reported tank Id {t.TankId} that is not a current WW2 console tank or not yiey on the database.");
                    expected.Remove(t.TankId);
                }
            }

            // WotcStat may be missing some tanks... just report. The reference values will come from XVM
            tanks = expected.AllTanks.Select(t => t.TankId).ToHashSet();
            foreach (var apiTank in apiTanks)
            {
                if (!tanks.Contains(apiTank.TankId))
                {
                    Log.Warn($"WotcStat didn't have expected values for {apiTank.ShortName}, id {apiTank.TankId}, Tier {apiTank.Tier}, {apiTank.Type}.");
                }
            }

            // Save WotcStat Data on DB and Calculate Console Values from it
            _recorder.Set(expected, Compute);
            Log.Info("Expected WotcStat Values saved.");
            if (Compute)
            {
                Log.Info("Console values computed.");
            }

            if (PutOnSite)
            {
                // Get the new calculated values and save on
                var wn8 = _provider.GetWn8ExpectedValues();

                if (!_putter.Put(wn8))
                {
                    throw new CommandException("Can't upload WN8 file!");
                }

                Log.Info("WN8 values saved to the site.");
            }

            console.Output.WriteLine("Done!");
            Log.Info($"Done {nameof(ImportWn8WotcStat)}.");

            return default;
        }
    }

}