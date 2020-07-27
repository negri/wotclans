using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using log4net;
using Negri.Wot.Sql;
using Newtonsoft.Json;

namespace Negri.Wot
{
    [Command("ImportXvm", Description = "Import Expected WN8 values from XVM")]
    public class ImportXvmCommand : ICommand
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ImportXvmCommand));

        private readonly Fetcher _fetcher;
        private readonly FtpPutter _ftpPutter;
        private readonly DbProvider _provider;
        private readonly DbRecorder _recorder;
        private readonly string _resultDirectory;

        public ImportXvmCommand(Fetcher fetcher, FtpPutter ftpPutter, DbProvider provider, DbRecorder recorder, string resultDirectory)
        {
            _fetcher = fetcher;
            _provider = provider;
            _recorder = recorder;
            _resultDirectory = resultDirectory;
            _ftpPutter = ftpPutter;
        }

        [CommandOption("WebCacheAge", 'a', Description = "Maximum age for data retrieved from the web")]
        public TimeSpan WebCacheAge { get; set; } = TimeSpan.FromHours(1);

        public ValueTask ExecuteAsync(IConsole console)
        {
            Log.Info($"Staring {nameof(ImportXvmCommand)}...");

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

            var json = JsonConvert.SerializeObject(wn8, Formatting.Indented);
            var file = Path.Combine(_resultDirectory, "MoE", $"{wn8.Date:yyyy-MM-dd}.WN8.json");
            File.WriteAllText(file, json, Encoding.UTF8);
            Log.Debug($"Saved WN8 Expected at '{file}'");

            _ftpPutter.PutExpectedWn8(file);

            console.Output.WriteLine("Done!");
            Log.Info($"Done {nameof(ImportXvmCommand)}.");
            return default;
        }
    }
}