using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using log4net;
using Negri.Wot.Sql;
using Newtonsoft.Json;

namespace Negri.Wot.Commands
{
    [Command("CalculateMoe", Description = "Calculate MoE for tanks")]
    public class CalculateMoe : ICommand
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CalculateMoe));

        private readonly Fetcher _fetcher;
        private readonly Putter _putter;
        private readonly DbProvider _provider;
        private readonly DbRecorder _recorder;
        
        public CalculateMoe(Fetcher fetcher, Putter putter, DbProvider provider, DbRecorder recorder)
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

        public ValueTask ExecuteAsync(IConsole console)
        {
            Log.Info($"Starting {nameof(CalculateMoe)}...");

            var csw = Stopwatch.StartNew();
            _recorder.CalculateMoE(UtcShiftToCalculate);
            csw.Stop();
            Log.Debug($"MoEs calculated in {csw.Elapsed.TotalSeconds:N0}s.");

            // Get the last one to see of there's the need to upload to the remote site
            var siteDiagnostic = _fetcher.GetSiteDiagnostic();
            var lastOnSite = siteDiagnostic.TanksMoELastDate;
            Log.Info($"Last MoEs on site: {lastOnSite:yyyy-MM-dd}");

            var dbDate = _provider.GetMoe().First().Date;
            Log.DebugFormat("MoEs on Database: {0:yyyy-MM-dd}", dbDate);

            var date = lastOnSite.AddDays(1);
            while (date <= dbDate)
            {
                Log.InfoFormat("Doing upload for {0:yyyy-MM-dd}...", date);
                var tankMarks = _provider.GetMoe(date).ToDictionary(t => t.TankId);

                if (tankMarks.Count > 0)
                {
                    if (!_putter.Put(date, tankMarks))
                    {
                        Log.Error($"Couldn't upload MoE file!");
                    }
                }
                else
                {
                    Log.Error($"There are 0 tanks with calculated MoEs in {date:yyyy-MM-dd}!");
                }

                date = date.AddDays(1);
            }

            console.Output.WriteLine("Done!");
            Log.Info($"Done {nameof(CalculateMoe)}.");

            return default;
        }

    }
}