using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using log4net;
using Negri.Wot.Sql;

namespace Negri.Wot.Commands
{
    [Command("CalculateClans", Description = "Calculates and uploads to the site by clan stats.")]
    public class CalculateClans : ICommand
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CalculateClans));

        private readonly FtpPutter _ftpPutter;
        private readonly DbProvider _provider;
        private readonly DbRecorder _recorder;
        private readonly string _resultDirectory;

        public CalculateClans(FtpPutter ftpPutter, DbProvider provider,
            DbRecorder recorder, string resultDirectory)
        {
            _provider = provider;
            _recorder = recorder;
            _resultDirectory = resultDirectory;
            _ftpPutter = ftpPutter;
        }

        [CommandParameter(0, Description = "Hours after a clan calculation being update that a new calculation should be done.")]
        public int AgeHours { get; set; } = 12;

        [CommandOption("WaitForUpload", Description = "If the program should wait for each clan upload to finish.")]
        public bool WaitForUpload { get; set; } = true;

        [CommandOption("MaxParallel", Description = "Maximum parallel threads.")]
        public int MaxParallel { get; set; } = 2;

        [CommandOption("MaxRunMinutes", Description = "Maximum run minutes.")]
        public int? MaxRunMinutes { get; set; }

        public ValueTask ExecuteAsync(IConsole console)
        {
            var sw = Stopwatch.StartNew();

            console.Output.WriteLine($"Starting {nameof(CalculateClans)}...");

            if (MaxRunMinutes == null)
            {
                MaxRunMinutes = 60 - DateTime.Now.Minute;
                if (MaxRunMinutes < 5)
                {
                    MaxRunMinutes = 5;
                }
            }

            Log.Info("------------------------------------------------------------------------------------");
            Log.Info($"{nameof(CalculateClans)} starting...");
            Log.Info($"AgeHours: {AgeHours}; resultDirectory: {_resultDirectory}; WaitForUpload: {WaitForUpload}; MaxRunMinutes: {MaxRunMinutes}");

            var clans = _provider.GetClanCalculateOrder(AgeHours).ToArray();
            Log.InfoFormat("{0} clans should be calculated.", clans.Length);

            var doneCount = 0;
            Parallel.For(0, clans.Length, new ParallelOptions { MaxDegreeOfParallelism = MaxParallel }, i =>
            {
                if (sw.Elapsed.TotalMinutes > MaxRunMinutes.Value)
                {
                    return;
                }

                var clan = clans[i];

                Log.DebugFormat("Calculating clan {0} of {1}: {2}...", i + 1, clans.Length, clan.ClanTag);
                var csw = Stopwatch.StartNew();

                var cc = CalculateClan(clan);

                Log.DebugFormat("Calculated clan {0} of {1}: {2} em {3:N1}s...", i + 1, clans.Length, clan.ClanTag, csw.Elapsed.TotalSeconds);

                if (cc != null)
                {
                    var fsw = Stopwatch.StartNew();

                    var fileName = cc.ToFile(_resultDirectory);
                    Log.Info($"Clan file wrote to '{fileName}'.");

                    var putTask = Task.Run(() =>
                    {
                        try
                        {
                            _ftpPutter.PutClan(fileName);
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"Error putting console clan file for {cc.ClanTag}", ex);
                        }
                    });

                    if (WaitForUpload)
                    {
                        putTask.Wait();
                    }

                    Log.DebugFormat("Clan upload {0} of {1}: {2} in {3:N1}s...", i + 1, clans.Length, clan.ClanTag, fsw.Elapsed.TotalSeconds);
                }

                Interlocked.Increment(ref doneCount);
                Log.DebugFormat("Done calculation of clan {0} of {1}: {2} in {3:N1}s. {4} clans done in total.", i + 1, clans.Length, clan.ClanTag, csw.Elapsed.TotalSeconds, doneCount);
            });

            Log.Debug("Execution complete.");
            console.Output.WriteLine("Done!");
            return default;
        }

        private Clan CalculateClan(ClanBaseInformation clan)
        {
            Log.DebugFormat("Calculating clan {0}...", clan.ClanTag);

            var cc = _provider.GetClan(clan.ClanId);

            if (cc == null || cc.Count <= 0)
            {
                Log.Warn($"The clan [{clan.ClanTag}]({clan.ClanId}) don't have their members uploaded yet.");
                return null;
            }

            Log.Info($"Calculated clan [{cc.ClanTag}]({cc.ClanId}): Members: {cc.Count}; Actives: {cc.Active}; WN8t15: {cc.Top15Wn8}");

            _recorder.SetClanCalculation(cc);

            return cc;
        }

    }
}