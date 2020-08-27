using System;
using System.Linq;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using log4net;
using Negri.Wot.Sql;
using Negri.Wot.Tanks;

namespace Negri.Wot.Commands
{
    [Command("ImportMoe", Description = "Import MoE numbers from WoTConsole.ru")]
    public class ImportMoe : ICommand
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ImportMoe));

        private readonly Fetcher _fetcher;
        private readonly DbRecorder _recorder;

        public ImportMoe(Fetcher fetcher, DbRecorder recorder)
        {
            _fetcher = fetcher;
            _recorder = recorder;
        }

        [CommandOption("WebCacheAge", Description = "Maximum age for data retrieved from the web")]
        public TimeSpan WebCacheAge { get; set; } = TimeSpan.FromHours(1);

        public ValueTask ExecuteAsync(IConsole console)
        {
            Log.Info($"Starting {nameof(ImportMoe)}...");

            _fetcher.WebCacheAge = WebCacheAge;

            // Get Console Tanks on the API
            var consoleTanks = _fetcher.GetTanks(Platform.Console).ToArray();
            Log.Info($"{consoleTanks.Length} Console Tanks retrieved from the API.");

            // Save on Database
            _recorder.Set(Platform.Console, consoleTanks);
            Log.Info("Console Tanks saved.");

            // Get WoTConsole.ru MoE Values
            var response = _fetcher.GetMoEFromWoTConsoleRu();
            Log.Info("MoE values retrieved from WoTConsole.ru");

            // Checking for bizarre ids..
            var tanks = consoleTanks.Select(t => t.TankId).ToHashSet();
            foreach (var t in response.data.Values.ToArray())
            {
                if (!tanks.Contains(t.TankId))
                {
                    Log.Warn($"WoTConsole.ru reported tank Id {t.TankId} that is not a current Console tank.");
                    response.data.Remove(t.TankId);
                }
            }

            // Save Data on DB
            _recorder.Set(MoeMethod.WoTConsoleRu, response.moment, response.data.Values.ToArray());
            Log.Info("MoE Values Saved.");


            console.Output.WriteLine("Done!");
            Log.Info($"Done {nameof(ImportMoe)}.");

            return default;
        }
    }
}