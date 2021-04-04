using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using log4net;
using Negri.Wot.Sql;

namespace Negri.Wot.Commands
{
    [Command("PurgeOldPlayers", Description = "Delete from database players that don't play anymore")]
    public class PurgeOldPlayers : ICommand
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(BalanceClans));

        private readonly DbRecorder _recorder;

        public PurgeOldPlayers(DbRecorder recorder)
        {
            _recorder = recorder;
        }

        [CommandParameter(0, Description = "Number of lunar months (28 days) of not playing any battle to consider a player inactive.")]
        public int Months { get; set; } = 13;

        public ValueTask ExecuteAsync(IConsole console)
        {
            Log.Info($"Starting {nameof(PurgeOldPlayers)}...");

            if (Months < 13)
            {
                Months = 13;
            }

            Log.Debug($"Purging players inactives for more than {Months} lunar months...");

            _recorder.PurgeOldPlayers(Months);

            Log.Info("Done.");
            return default;
        }
    }
}