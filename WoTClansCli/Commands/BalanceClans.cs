using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using log4net;
using Negri.Wot.Sql;

namespace Negri.Wot.Commands
{
    [Command("BalanceClans", Description = "Balance the schedule of clans data retrieving")]
    public class BalanceClans : ICommand
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(BalanceClans));

        private readonly DbProvider _provider;
        private readonly DbRecorder _recorder;
        
        public BalanceClans(DbProvider provider, DbRecorder recorder)
        {
            _provider = provider;
            _recorder = recorder;
        }

        [CommandParameter(0, Description = "Players per minute, on average, to be retrieved.")]
        public int PlayersPerMinute { get; set; } = 20;

        public ValueTask ExecuteAsync(IConsole console)
        {
            Log.Info($"Starting {nameof(BalanceClans)}...");

            Log.Debug("Retrieving data diagnostics...");
            var dd = _provider.GetDataDiagnostic();

            Log.Info($"Scheduled Players Per Day: {dd.ScheduledPlayersPerDay:N0}");
            Log.Info($"Target Players Per Minute: {PlayersPerMinute:N0}");

            // Minutes, in average, per day, that the system is off, or not retrieving data
            const int averageOutTimeMinutes = 30;

            // The average clan size, as retrieved in 2020-07-30
            const int averageClanSize = 14;

            var maxPerDay = PlayersPerMinute * (60 * 24 - averageOutTimeMinutes);
            var minPerDay = maxPerDay - 2*averageClanSize;

            Log.Info($"Min players per day: {minPerDay:N0}");
            Log.Info($"Max players per day: {maxPerDay:N0}");
            
            if (dd.ScheduledPlayersPerDay < minPerDay || dd.ScheduledPlayersPerDay > maxPerDay)
            {
                Log.Info($"Balancing between {minPerDay:N0} and {maxPerDay:N0}.");
                _recorder.BalanceClanSchedule(minPerDay, maxPerDay);
            }

            console.Output.WriteLine("Done!");
            Log.Info($"Done {nameof(BalanceClans)}.");

            return default;
        }
    }
}