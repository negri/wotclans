using System;
using System.Configuration;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using log4net;
using Negri.Wot.Sql;

namespace Negri.Wot.Bot
{
    [Group("coder")]
    [Description("Commands that only the maintainer of the WoTClans can issue.")]
    [Hidden]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public class CoderCommands : CommandsBase
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CoderCommands));
        private readonly ulong _coder;
        private readonly string _connectionString;

        public CoderCommands()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;
            _coder = ulong.Parse(ConfigurationManager.AppSettings["Coder"] ?? "0");
        }

        [Command("db")]
        [Description("Retrieve the database status.")]
        public async Task GetDbStatus(CommandContext ctx)
        {
            await ctx.TriggerTypingAsync();

            var userId = ctx.User?.Id ?? 0;

            Log.Info($"Requesting {nameof(GetDbStatus)} by {userId}...");
            if (userId != _coder)
            {
                var emoji = DiscordEmoji.FromName(ctx.Client, ":no_entry:");
                var embed = new DiscordEmbedBuilder
                {
                    Title = "Access denied",
                    Description = $"{emoji} You may be a *coder*, but you are not **The Coder**!",
                    Color = DiscordColor.Red
                };

                await ctx.RespondAsync("", embed: embed);
                return;
            }

            try
            {
                var provider = new DbProvider(_connectionString);
                var s = provider.GetDataDiagnostic();

                var sb = new StringBuilder();

                sb.AppendLine($"Scheduled Players Per Hour: {s.ScheduledPlayersPerHour:N0}");
                sb.AppendLine($"Last Hour Players: {s.AvgPlayersPerHourLastHour:N0}");
                sb.AppendLine($"Avg Players Per Hour Last 2 Hours: {s.AvgPlayersPerHourLast2Hours:N0}");
                sb.AppendLine($"Avg Players Per Hour Last 6 Hours: {s.AvgPlayersPerHourLast6Hours:N0}");
                sb.AppendLine($"Avg Players Per Hour Last Day: {s.AvgPlayersPerHourLastDay:N0}");
                sb.AppendLine();
                sb.AppendLine(
                    $"Delay on the last 48h/72h/96h: {s.Last48HDelay:N1}; {s.Last72HDelay:N1}; {s.Last96HDelay:N1}");
                sb.AppendLine($"Total Players: {s.TotalPlayers:N0}; Enabled Clans: {s.TotalEnabledClans};");
                sb.AppendLine($"Players Queue Length: {s.PlayersQueueLenght}");
                sb.AppendLine($"Membership Queue Length: {s.MembershipQueueLenght}");
                sb.AppendLine($"Calculate Queue Length: {s.CalculateQueueLenght}");

                var embed = new DiscordEmbedBuilder
                {
                    Title = "Database Status",
                    Description = sb.ToString(),
                    Color = DiscordColor.Goldenrod,
                    Author = new DiscordEmbedBuilder.EmbedAuthor
                    {
                        Name = "WoTClans",
                        Url = "https://wotclans.com.br"
                    },
                    Footer = new DiscordEmbedBuilder.EmbedFooter
                    {
                        Text = $"Retrieved at {s.Moment:yyyy-MM-dd HH:mm} UTC."
                    }
                };

                await ctx.RespondAsync("", embed: embed);

                Log.Debug($"{nameof(GetDbStatus)} returned ok.");
            }
            catch (Exception ex)
            {
                Log.Error($"{nameof(GetDbStatus)}", ex);
                await ctx.RespondAsync(
                    $"Sorry, {ctx.User?.Mention}. There was an error... the *Coder* will be notified of `{ex.Message}`.");
            }
        }   

        [Command("PurgeTanker")]
        [Description("Purge a player by Wargaming ID.")]
        public async Task PurgePlayer(CommandContext ctx, [Description("Player Id")] long playerId)
        {
            await ctx.TriggerTypingAsync();

            var userId = ctx.User?.Id ?? 0;

            Log.Info($"Requesting {nameof(GetPlayerById)}({playerId}) by {userId}...");
            if (userId != _coder)
            {
                var emoji = DiscordEmoji.FromName(ctx.Client, ":no_entry:");
                var embed = new DiscordEmbedBuilder
                {
                    Title = "Access denied",
                    Description = $"{emoji} You may be a *coder*, but you are not **The Coder**!",
                    Color = DiscordColor.Red
                };

                await ctx.RespondAsync("", embed: embed);
                return;
            }

            try
            {
                var provider = new DbProvider(_connectionString);
                var player = provider.GetPlayer(playerId);
                if (player == null)
                {
                    await ctx.RespondAsync($"There is no player on the database with the id `{playerId}`.");
                    return;
                }

                await ctx.RespondAsync($"The id `{playerId}` corresponds to the tanker `{player.Name}` on the `{player.Plataform}`...");

                var recorder = new DbRecorder(_connectionString);
                recorder.PurgePlayer(playerId);

                await ctx.RespondAsync($"The tanker `{player.Name}` on `{player.Plataform}` was purged from the database. Rip.");
            }
            catch (Exception ex)
            {
                Log.Error($"{nameof(GetDbStatus)}", ex);
                await ctx.RespondAsync(
                    $"Sorry, {ctx.User?.Mention}. There was an error... the *Coder* will be notified of `{ex.Message}`.");
            }
        }

        [Command("TankerById")]
        [Description("Retrieve the game tag by Wargaming ID.")]
        public async Task GetPlayerById(CommandContext ctx, [Description("Player Id")] long playerId)
        {
            await ctx.TriggerTypingAsync();

            var userId = ctx.User?.Id ?? 0;

            Log.Info($"Requesting {nameof(GetPlayerById)}({playerId}) by {userId}...");
            if (userId != _coder)
            {
                var emoji = DiscordEmoji.FromName(ctx.Client, ":no_entry:");
                var embed = new DiscordEmbedBuilder
                {
                    Title = "Access denied",
                    Description = $"{emoji} You may be a *coder*, but you are not **The Coder**!",
                    Color = DiscordColor.Red
                };

                await ctx.RespondAsync("", embed: embed);
                return;
            }

            try
            {
                var provider = new DbProvider(_connectionString);
                var player = provider.GetPlayer(playerId);
                if (player == null)
                {
                    await ctx.RespondAsync($"There is no player on the database with the id `{playerId}`.");
                    return;
                }

                await ctx.RespondAsync($"The id `{playerId}` corresponds to the tanker `{player.Name}` on the `{player.Plataform}`.");
            }
            catch (Exception ex)
            {
                Log.Error($"{nameof(GetDbStatus)}", ex);
                await ctx.RespondAsync(
                    $"Sorry, {ctx.User?.Mention}. There was an error... the *Coder* will be notified of `{ex.Message}`.");
            }
        }

        [Command("site")]
        [Description("Retrieve the site status.")]
        public async Task GetSiteStatus(CommandContext ctx, [Description("The site to query, *PS* or *XBOX*")]
            string platformString = "XBOX")
        {
            await ctx.TriggerTypingAsync();

            var userId = ctx.User?.Id ?? 0;

            Log.Info($"Requesting {nameof(GetSiteStatus)} by {userId}...");
            if (userId != _coder)
            {
                var emoji = DiscordEmoji.FromName(ctx.Client, ":no_entry:");
                var embed = new DiscordEmbedBuilder
                {
                    Title = "Access denied",
                    Description = $"{emoji} You may be a *coder*, but you are not **The Coder**!",
                    Color = DiscordColor.Red
                };

                await ctx.RespondAsync("", embed: embed);
                return;
            }

            var cfg = GuildConfiguration.FromGuild(ctx.Guild);
            var platform = GetPlatform(platformString + ".", cfg.Plataform, out _);

            try
            {
                var cacheDirectory = ConfigurationManager.AppSettings["CacheDir"] ?? Path.GetTempPath();
                var webCacheAge = TimeSpan.FromHours(4);
                var appId = ConfigurationManager.AppSettings["WgAppId"] ?? "demo";

                var fetcher = new Fetcher(cacheDirectory)
                {
                    ApplicationId = appId,
                    WebCacheAge = webCacheAge,
                    WebFetchInterval = TimeSpan.FromSeconds(1)
                };

                var s = fetcher.GetSiteDiagnostic(platform, ConfigurationManager.AppSettings["ApiAdminKey"]);

                var sb = new StringBuilder();
                sb.AppendLine($"Data Age Minutes: {s.DataAgeMinutes:N0}");
                sb.AppendLine($"Most Recent Clan Moment: {s.MostRecentClanMoment:yyyy-MM-dd HH:mm}");
                sb.AppendLine($"Tank Leaders Last Date: {s.TankLeadersLastDate:yyyy-MM-dd}");
                sb.AppendLine($"Tank MoE Last Date: {s.TanksMoELastDate:yyyy-MM-dd}");
                sb.AppendLine($"Players: {s.PlayersCount:N0}; Clans: {s.ClansCount:N0}");
                sb.AppendLine($"Clans With Players Updated On Last Hour: {s.ClansWithPlayersUpdatedOnLastHour:N0}");
                sb.AppendLine($"Since Started Load: {s.AveragedProcessCpuUsage.SinceStartedLoad:P1}");

                var platformPrefix = platform == Platform.PS ? "ps." : string.Empty;

                var embed = new DiscordEmbedBuilder
                {
                    Title = $"Site Status - {platform.ToString().ToUpperInvariant()}",
                    Description = sb.ToString(),
                    Color = DiscordColor.Goldenrod,
                    Author = new DiscordEmbedBuilder.EmbedAuthor
                    {
                        Name = "WoTClans",
                        Url = $"https://{platformPrefix}wotclans.com.br"
                    },
                    Footer = new DiscordEmbedBuilder.EmbedFooter
                    {
                        Text = $"Retrieved at {s.ServerMoment:yyyy-MM-dd HH:mm} UTC."
                    }
                };

                await ctx.RespondAsync("", embed: embed);

                Log.Debug($"{nameof(GetSiteStatus)} returned ok.");
            }
            catch (Exception ex)
            {
                Log.Error($"{nameof(GetSiteStatus)}", ex);
                await ctx.RespondAsync(
                    $"Sorry, {ctx.User?.Mention}. There was an error... the *Coder* will be notified of `{ex.Message}`.");
            }
        }

        [Command("SetClan")]
        [Description("Set properties for a clan")]
        public async Task SetClan(CommandContext ctx, [Description("The clan tag")] string clanTag,
            [Description("The clan flag")] string flagCode = null,
            [Description("Enable or Disable the Clan")]
            bool enable = true,
            [Description("To ban or not a clan from the site")]
            bool isBan = false)
        {
            await ctx.TriggerTypingAsync();

            var userId = ctx.User?.Id ?? 0;

            Log.Info($"Requesting {nameof(SetClan)} by {userId}...");
            if (userId != _coder)
            {
                var emoji = DiscordEmoji.FromName(ctx.Client, ":no_entry:");
                var embed = new DiscordEmbedBuilder
                {
                    Title = "Access denied",
                    Description = $"{emoji} You may be a *coder*, but you are not **The Coder**!",
                    Color = DiscordColor.Red
                };

                await ctx.RespondAsync("", embed: embed);
                return;
            }

            var cfg = GuildConfiguration.FromGuild(ctx.Guild);
            var platform = GetPlatform(clanTag, cfg.Plataform, out clanTag);

            clanTag = clanTag.Trim('[', ']');
            clanTag = clanTag.ToUpperInvariant();

            if (!ClanTagRegex.IsMatch(clanTag))
            {
                await ctx.RespondAsync($"You must send a **valid** clan **tag** as parameter, {ctx.User?.Mention}.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(flagCode))
            {
                flagCode = flagCode.RemoveDiacritics().ToUpperInvariant();

                if (flagCode.Length != 2)
                {
                    await ctx.RespondAsync($"The flag code must be 2 letters only, {ctx.User?.Mention}.");
                    return;
                }
            }

            Log.Warn($"{nameof(SetClan)}({clanTag}, {platform}, {flagCode}, {enable}, {isBan})...");

            try
            {
                await ctx.TriggerTypingAsync();

                var provider = new DbProvider(_connectionString);
                var recorder = new DbRecorder(_connectionString);

                var cacheDirectory = ConfigurationManager.AppSettings["CacheDir"] ?? Path.GetTempPath();
                var webCacheAge = TimeSpan.FromHours(4);
                var appId = ConfigurationManager.AppSettings["WgAppId"] ?? "demo";

                var fetcher = new Fetcher(cacheDirectory)
                {
                    ApplicationId = appId,
                    WebCacheAge = webCacheAge,
                    WebFetchInterval = TimeSpan.FromSeconds(1)
                };

                var clan = provider.GetClan(platform, clanTag);
                
                if (clan == null && enable)
                {
                    // Check to add...
                    await ctx.RespondAsync($"Not found `{clanTag}` on the database. Searching the WG API...");
                    await ctx.TriggerTypingAsync();

                    var clanOnSite = fetcher.FindClan(platform, clanTag, true);
                    if (clanOnSite == null)
                    {
                        await ctx.RespondAsync(
                            $"Not found `{clanTag}` on the WG API for `{platform}`. Check the clan tag.");
                        return;
                    }

                    if (clanOnSite.AllMembersCount < 7)
                    {
                        await ctx.RespondAsync(
                            $"The clan `{clanTag}` on `{platform}` has only {clanOnSite.AllMembersCount}, and will not be added to the system.");
                        return;
                    }

                    clanOnSite.Country = flagCode;
                    recorder.Add(clanOnSite);

                    await ctx.RespondAsync(
                        $"The clan `{clanTag}` on `{platform}` with {clanOnSite.AllMembersCount} members was added to the system and " +
                        "should appear on the site in ~12 hours. Keep playing to achieve at least 7 members with 21 recent battles and appear on the default view.");

                    Log.Info($"Added {platform}.{clanTag}");
                    return;
                }

                if (clan == null)
                {
                    await ctx.RespondAsync(
                        $"Not found `{clanTag}` on `{platform}`. Check the clan tag.");
                    return;
                }

                if (!clan.Enabled && enable)
                {
                    // Can be enabled?
                    var clanOnSite = fetcher.GetClans(new[] {clan}).FirstOrDefault();
                    if (clanOnSite == null)
                    {
                        await ctx.RespondAsync(
                            $"Not found `{clanTag}` on the WG API for `{platform}`. Check the clan tag.");
                        return;
                    }

                    if (clanOnSite.IsDisbanded)
                    {
                        await ctx.RespondAsync($"The clan `{clanTag}` on `{platform}` was disbanded.");
                        return;
                    }

                    if (clanOnSite.Count < 7)
                    {
                        await ctx.RespondAsync(
                            $"The clan `{clanTag}` on `{platform}` has only {clanOnSite.Count} members and will not be enabled.");
                        return;
                    }

                    if (clan.DisabledReason == DisabledReason.Banned)
                    {
                        await ctx.RespondAsync(
                            $"The clan `{clanTag}` ({clan.ClanId}) on `{platform}` was **banned** from the site.");
                        return;
                    }

                    recorder.EnableClan(clanOnSite.Plataform, clanOnSite.ClanId);
                    await ctx.RespondAsync(
                        $"The clan `{clanTag}` on `{platform}` disabled for `{clan.DisabledReason}` is enabled again.");
                    Log.Info($"Enabled {platform}.{clanTag}");
                }
                else if (clan.Enabled && !enable)
                {
                    if (isBan)
                    {
                        recorder.DisableClan(clan.Plataform, clan.ClanId, DisabledReason.Banned);
                        await ctx.RespondAsync(
                            $"The clan `{clanTag}` ({clan.ClanId}) on `{platform}` was **BANNED** from the site.");
                        Log.Warn($"BANNED {platform}.{clanTag}");
                    }
                    else
                    {
                        recorder.DisableClan(clan.Plataform, clan.ClanId, DisabledReason.Unknow);
                        await ctx.RespondAsync(
                            $"The clan `{clanTag}` ({clan.ClanId}) on `{platform}` was **disabled** from the site.");
                        Log.Warn($"Disabled {platform}.{clanTag}");
                    }
                }

                // change flag?
                flagCode = flagCode ?? string.Empty;
                if (!string.Equals(flagCode, clan.Country ?? string.Empty, StringComparison.InvariantCultureIgnoreCase))
                {
                    recorder.SetClanFlag(clan.Plataform, clan.ClanId, flagCode.ToLowerInvariant());
                    await ctx.RespondAsync(
                        $"The flag of the clan `{clanTag}` on `{platform}` was changed to `{flagCode}`.");
                    Log.Info($"Flag changed on {platform}.{clanTag} to {flagCode}.");
                }

                await ctx.RespondAsync($"all done for `{clan.ClanTag}` on `{platform}`.");
            }
            catch (Exception ex)
            {
                Log.Error($"{nameof(SetClan)}", ex);
                await ctx.RespondAsync(
                    $"Sorry, {ctx.User.Mention}. There was an error... the *Coder* will be notified of `{ex.Message}`.");
            }
        }
    }
}