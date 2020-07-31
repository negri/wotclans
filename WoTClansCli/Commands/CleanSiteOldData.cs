using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using log4net;

namespace Negri.Wot.Commands
{
    [Command("CleanSiteOldData", Description = "Triggers the cleaning of old data on the site")]
    public class CleanSiteOldData : ICommand
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CleanSiteOldData));

        private readonly Putter _putter;

        public CleanSiteOldData(Putter putter)
        {
            _putter = putter;
        }

        [CommandOption("DaysToKeepOnDaily", Description = "Days to keep data that are updated daily")]
        public int DaysToKeepOnDaily { get; set; } = 4 * 7 + 7;

        [CommandOption("DaysToKeepOnWeekly", Description = "Days to keep data that are updated weekly")]
        public int DaysToKeepOnWeekly { get; set; } = 3 * 4 * 7 + 7;

        [CommandOption("DaysToKeepClans", Description = "Days to keep old clans data")]
        public int DaysToKeepClans { get; set; } = 2 * 4 * 7 + 7;

        [CommandOption("DaysToKeepPlayers", Description = "Days to keep old players data")]
        public int DaysToKeepPlayers { get; set; } = 2 * 4 * 7 + 7;

        public ValueTask ExecuteAsync(IConsole console)
        {
            Log.Info($"Starting {nameof(CleanSiteOldData)}...");

            var res = _putter.CleanOldData(DaysToKeepOnDaily, DaysToKeepOnWeekly, DaysToKeepClans, DaysToKeepPlayers);
            if (res == null)
            {
                Log.Error("The remote call failed.");
            }
            else
            {
                Log.Info($"Time Taken: {res.Elapsed}");
                Log.Info($"Files Deleted: {res.Deleted}");
                Log.Info($"Bytes Deleted: {res.DeletedMBytes:N1}MB");

                foreach (var e in res.Errors)
                {
                    Log.Error($"Remote error: {e}");
                }
            }

            console.Output.WriteLine("Done!");
            Log.Info($"Done {nameof(CleanSiteOldData)}.");

            return default;
        }
    }
}