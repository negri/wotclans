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
    [Command("ImportXvm", Description = "Import Expected WN8 values from XVM")]
    public class ImportXvm : ICommand
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ImportXvm));

        private readonly Fetcher _fetcher;
        private readonly Putter _putter;
        private readonly DbProvider _provider;
        private readonly DbRecorder _recorder;
        
        public ImportXvm(Fetcher fetcher, Putter putter, DbProvider provider, DbRecorder recorder)
        {
            _fetcher = fetcher;
            _provider = provider;
            _recorder = recorder;
            _putter = putter;
        }

        [CommandOption("WebCacheAge",  Description = "Maximum age for data retrieved from the web")]
        public TimeSpan WebCacheAge { get; set; } = TimeSpan.FromHours(1);

        public ValueTask ExecuteAsync(IConsole console)
        {
            Log.Info($"Starting {nameof(ImportXvm)}...");

            _fetcher.WebCacheAge = WebCacheAge;

            // Get PC Tanks on the API
            var pcTanks = _fetcher.GetTanks(Platform.PC).ToArray();
            Log.Info($"{pcTanks.Length} PC Tanks retrieved from the API.");

            // Save on Database
            _recorder.Set(Platform.PC, pcTanks);
            Log.Info("PC Tanks saved.");

            // Get XVM Expected Values
            var expected = _fetcher.GetXvmWn8ExpectedValues();
            Log.Info("Expected PC values retrieved from XVM");

            // XVM is returning some strange ids..
            var tanks = pcTanks.Select(t => t.TankId).ToHashSet();
            foreach (var t in expected.AllTanks)
            {
                if (!tanks.Contains(t.TankId))
                {
                    Log.Warn($"XVM reported tank Id {t.TankId} that is not a current PC tank.");
                    expected.Remove(t.TankId);
                }
            }

            // Save XVM Data on DB and Calculate Console Values from it
            _recorder.Set(expected);
            Log.Info("Expected Console Values Calculated.");

            // Get the new calculated values and save on
            var wn8 = _provider.GetWn8ExpectedValues();

            if (!_putter.Put(wn8))
            {
                throw new CommandException("Can't upload WN8 file!");
            }

            console.Output.WriteLine("Done!");
            Log.Info($"Done {nameof(ImportXvm)}.");

            return default;
        }
    }
}