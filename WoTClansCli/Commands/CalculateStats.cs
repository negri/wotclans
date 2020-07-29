using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using log4net;
using Negri.Wot.Sql;

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

        public ValueTask ExecuteAsync(IConsole console)
        {
            Log.Info($"Starting {nameof(CalculateStats)}...");

            console.Output.WriteLine("Done!");
            Log.Info($"Done {nameof(CalculateStats)}.");

            return default;
        }
    }
}