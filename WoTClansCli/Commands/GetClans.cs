using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using log4net;
using Negri.Wot.Sql;

namespace Negri.Wot.Commands
{
    [Command("GetClans", Description = "Fetch clans membership, add clans and so on.")]
    public class GetClans : ICommand
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(GetClans));

        private readonly Fetcher _fetcher;
        private readonly FtpPutter _ftpPutter;
        private readonly Putter _putter;
        private readonly DbProvider _provider;
        private readonly DbRecorder _recorder;
        private readonly string _resultDirectory;

        public GetClans(Fetcher fetcher, FtpPutter ftpPutter, Putter putter, DbProvider provider,
            DbRecorder recorder, string resultDirectory)
        {
            _fetcher = fetcher;
            _provider = provider;
            _recorder = recorder;
            _resultDirectory = resultDirectory;
            _ftpPutter = ftpPutter;
            _putter = putter;
        }

        [CommandParameter(0, Description = "Hours after a clan membership being update that new members should be fetched.")]
        public int AgeHours { get; set; } = 12;

        [CommandParameter(1, Description = "Maximum number of clans to be processed.")]
        public int MaxClans { get; set; } = 100;

        [CommandOption("WebFetchInterval", Description = "Interval between web queries on APIs.")]
        public TimeSpan WebFetchInterval { get; set; } = TimeSpan.Zero;

        [CommandOption("HourToAdd", Description = "Hour, on UTC, to search and and new clans.")]
        public int HourToAdd { get; set; } = 17;

        [CommandOption("MinClanSize", Description = "Minimum number of members for a clan to be added.")]
        public int MinClanSize { get; set; } = 7;

        [CommandOption("MaxToAdd", Description = "Maximum number of clans to be added.")]
        public int MaxToAutoAdd { get; set; } = 10;

        public ValueTask ExecuteAsync(IConsole console)
        {
            console.Output.WriteLine($"Starting {nameof(GetClans)}...");

            Log.Info("------------------------------------------------------------------------------------");
            Log.Info("GetClans iniciando...");
            Log.InfoFormat("ageHours: {0}; maxClans: {1}; webFetchInterval:{2}",
                AgeHours, MaxClans, WebFetchInterval);

            _fetcher.WebCacheAge = TimeSpan.FromHours(AgeHours - 1);

            if (MaxToAutoAdd > 0 && DateTime.UtcNow.Hour == HourToAdd)
            {
                AutomaticallyAddClans();
            }

            var clans = _provider.GetClanMembershipUpdateOrder(MaxClans, AgeHours).ToArray();
            if (clans.Length == 0)
            {
                Log.Info("No clan requires update.");
                return default;
            }
            Log.InfoFormat("{0} clans should be updated.", clans.Length);

            var clansToRename = new List<Clan>();
            var clansToUpdate = _fetcher.GetClans(clans).ToArray();

            // Disbanded Clans
            var disbanded = clansToUpdate.Where(c => c.IsDisbanded).ToArray();
            Log.Info($"{disbanded.Length} clans where disbanded.");
            foreach (var clan in disbanded)
            {
                Log.Debug($"Marking [{clan.ClanTag}]({clan.ClanId}) as disbanded...");
                var disbandedClan = _provider.GetClan(clan.ClanId);
                _recorder.DisableClan(disbandedClan.ClanId, DisabledReason.Disbanded);
                _putter.DeleteClan(disbandedClan.ClanTag);
            }

            // Too small clans
            var small = clansToUpdate.Where(c => !c.IsDisbanded && c.Count < 4).ToArray();
            Log.Info($"Disabling {small.Length} clans that went too small.");
            foreach (var clan in small)
            {
                Log.Debug($"Disabling [{clan.ClanTag}]({clan.ClanId}) as too small ({clan.Count} members)...");
                _recorder.DisableClan(clan.ClanId, DisabledReason.TooSmall);
            }

            // Save the normal clans
            var normal = clansToUpdate.Where(c => !c.IsDisbanded && c.Count >= 4).ToArray();
            foreach (var clan in normal)
            {
                Log.Debug($"Saving membership for clan [{clan.ClanTag}].({clan.ClanId}) with {clan.Count} members...");
                _recorder.Set(clan, true);
                if (clan.HasChangedTag)
                {
                    clansToRename.Add(clan);
                }
            }

            Log.Info($"{clansToRename.Count} changed their tags.");
            if (clansToRename.Any())
            {
                var resultDirectory = Path.Combine(_resultDirectory, "Clans");

                foreach (var clan in clansToRename)
                {
                    Log.InfoFormat("The clan {0}.{1} had the tag changed from {2}.", clan.ClanId, clan.ClanTag, clan.OldTag);

                    // Faz copia do arquivo local, e o upload com o novo nome
                    var oldFile = Path.Combine(resultDirectory, $"clan.{clan.OldTag}.json");
                    if (!File.Exists(oldFile))
                    {
                        continue;
                    }

                    var newFile = Path.Combine(resultDirectory, $"clan.{clan.ClanTag}.json");
                    File.Copy(oldFile, newFile, true);

                    try
                    {
                        _ftpPutter.PutClan(newFile);
                        _ftpPutter.DeleteFile($"Clans/clan.{clan.OldTag}.json");
                        _ftpPutter.SetRenameFile(clan.OldTag, clan.ClanTag);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Error renaming clan {clan.OldTag} to {clan.ClanTag} on the site.", ex);
                    }
                }
            }

            Log.Debug("Execution complete.");
            console.Output.WriteLine("Done!");
            return default;
        }

        private void AutomaticallyAddClans()
        {
            var toAdd = _fetcher.GetClans(MinClanSize).ToList();
            Log.Info($"{toAdd.Count} clans with at least {MinClanSize} members.");

            var clanIds = _provider.EnumClans().Select(c => c.ClanId).ToHashSet();

            toAdd = toAdd.Where(c => !clanIds.Contains(c.ClanId))
                .OrderByDescending(c => c.AllMembersCount).ToList();
            Log.Debug($"{toAdd.Count} clans with at least {MinClanSize} are new to the system. The limit to add is {MaxToAutoAdd}");

            foreach (var c in toAdd.Take(MaxToAutoAdd))
            {
                _recorder.Add(c);
                Log.Info($"Added clan [{c.ClanTag}]({c.ClanId}) with {c.AllMembersCount} members.");
            }

            Log.Debug($"Re-adding up to {MaxToAutoAdd} disabled clans...");
            _recorder.ReAddClans(MaxToAutoAdd);
        }
    }
}