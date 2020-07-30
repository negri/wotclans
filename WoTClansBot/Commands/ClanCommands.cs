using System;
using System.Collections.Concurrent;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using log4net;
using Negri.Wot.Sql;

namespace Negri.Wot.Bot
{
    public class ClanCommands : CommandsBase
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ClanCommands));

        private readonly string _connectionString;

        public ClanCommands()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;
        }

        [Command("clanInactives")]
        [Aliases("Inactives")]
        [Description("The clan's inactives players")]
        public async Task ClanInactives(CommandContext ctx, [Description("The clan **tag**")] string clanTag)
        {
            try
            {
                if (!await CanExecute(ctx, Features.Clans))
                {
                    return;
                }

                await ctx.TriggerTypingAsync();

                if (string.IsNullOrWhiteSpace(clanTag))
                {
                    await ctx.RespondAsync($"You must send a clan tag as parameter, {ctx.User.Mention}.");
                    return;
                }

                Log.Debug($"Requesting {nameof(ClanInactives)}({clanTag})...");

                clanTag = clanTag.Trim('[', ']');
                clanTag = clanTag.ToUpperInvariant();

                if (!ClanTagRegex.IsMatch(clanTag))
                {
                    await ctx.RespondAsync($"You must send a **valid** clan **tag** as parameter, {ctx.User.Mention}.");
                    return;
                }

                var provider = new DbProvider(_connectionString);

                var clan = provider.GetClan(clanTag);
                if (clan == null)
                {
                    await ctx.RespondAsync(
                        $"Can't find on a clan with tag `[{clanTag}]`, {ctx.User.Mention}. Maybe my site doesn't track it yet... or you have the wrong clan tag.");
                    return;
                }

                if (!clan.Enabled)
                {
                    await ctx.RespondAsync($"Data collection for this clan is **disabled**, the reason for this is {clan.DisabledReason}, {ctx.User.Mention}.");
                    return;
                }

                var inactives = clan.Players.Where(p => !p.IsActive).ToArray();
                if (inactives.Length <= 0)
                {
                    await ctx.RespondAsync($"There are no inactive tankers on this clan, {ctx.User.Mention}.");
                    return;
                }

                // To retrieve the last battle
                for (int i = 0; i < inactives.Length; i++)
                {
                    inactives[i] = provider.GetPlayer(inactives[i].Id, true);
                }

                inactives = inactives.OrderBy(p => p.MonthBattles).ThenBy(p => p.LastBattle ?? DateTime.Today.AddYears(-5)).ToArray();

                var sb = new StringBuilder();

                sb.Append($"Information about `{clan.ClanTag}`'s {inactives.Length} inactives tankers,  {ctx.User.Mention}:");
                sb.AppendLine();

                var maxNameLength = inactives.Max(p => p.Name.Length);

                sb.AppendLine("```");
                sb.AppendLine($"{"Tanker".PadRight(maxNameLength)} {"Days",5} {"Battles",7} {"WN8",6}");
                foreach (var p in inactives.Take(30))
                {
                    sb.AppendLine($"{(p.Name ?? string.Empty).PadRight(maxNameLength)} {(DateTime.UtcNow - (p.LastBattle ?? DateTime.Today.AddYears(-5))).TotalDays.ToString("N0").PadLeft(5)} {p.MonthBattles.ToString("N0").PadLeft(7)} {p.TotalWn8.ToString("N0").PadLeft(6)}");
                }
                sb.AppendLine("```");

                var color = clan.InactivesWn8.ToColor();
                
                var embed = new DiscordEmbedBuilder
                {
                    Title = $"{clan.ClanTag}'s Inactives Tankers",
                    Description = sb.ToString(),
                    Color = new DiscordColor(color.R, color.G, color.B),
                    Url = $"https://wotclans.com.br/Clan/{clan.ClanTag}",
                    Author = new DiscordEmbedBuilder.EmbedAuthor
                    {
                        Name = "WoTClans",
                        Url = "https://wotclans.com.br"
                    },
                    Footer = new DiscordEmbedBuilder.EmbedFooter
                    {
                        Text = $"Calculated at {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC"
                    }
                };

                await ctx.RespondAsync("", embed: embed);

            }
            catch (Exception ex)
            {
                Log.Error($"Error calling {nameof(ClanInactives)}({clanTag})", ex);
                await ctx.RespondAsync(
                    $"Sorry, {ctx.User.Mention}. There was an error... the *Coder* will be notified of `{ex.Message}`.");
                
            }
        }


        [Command("clanTopOnTank")]
        [Description("The clan's best players on a given tank")]
        public async Task ClanTopOnTank(CommandContext ctx, 
            [Description("The clan **tag**")] string clanTag,
            [Description("The Tank name, as it appears in battles. If it has spaces, enclose it on quotes.")][RemainingText] string tankName)
        {
            if (!await CanExecute(ctx, Features.Clans))
            {
                return;
            }

            await ctx.TriggerTypingAsync();

            if (string.IsNullOrWhiteSpace(clanTag))
            {
                await ctx.RespondAsync($"You must send a clan tag as parameter, {ctx.User.Mention}.");
                return;
            }

            Log.Debug($"Requesting {nameof(ClanTopOnTank)}({clanTag}, {tankName})...");

            if (!ClanTagRegex.IsMatch(clanTag))
            {
                await ctx.RespondAsync($"You must send a **valid** clan **tag** as parameter, {ctx.User.Mention}.");
                return;
            }

            var provider = new DbProvider(_connectionString);

            var clan = provider.GetClan(clanTag);
            if (clan == null)
            {
                await ctx.RespondAsync(
                    $"Can't find on a clan with tag `[{clanTag}]`, {ctx.User.Mention}. Maybe my site doesn't track it yet... or you have the wrong clan tag.");
                return;
            }

            if (!clan.Enabled)
            {
                await ctx.RespondAsync(
                        $"Data collection for the `[{clan.ClanTag}]` is disabled, {ctx.User.Mention}. Maybe the clan went too small, or inactive.");
                return;
            }

            var tankCommands = new TankCommands();
            var tank = tankCommands.FindTank(tankName, out _);

            if (tank == null)
            {
                await ctx.RespondAsync($"Can't find a tank with `{tankName}` on the name, {ctx.User.Mention}.");
                return;
            }

            var tr = provider.GetTanksReferences(null, tank.TankId, includeMoe: false, includeHistogram: false, includeLeaders: false).FirstOrDefault();

            if (tr == null)
            {
                await ctx.RespondAsync($"Sorry, there is no tank statistics for the `{tank.Name}`, {ctx.User.Mention}.");
                return;
            }

            if (tr.Tier < 5)
            {
                await ctx.RespondAsync($"Sorry, this command is meant to be used only with tanks Tier 5 and above, {ctx.User.Mention}.");
                return;
            }

            var players = provider.GetClanPlayerIdsOnTank(clan.ClanId, tr.TankId).ToList();
            if (players.Count <= 0)
            {
                await ctx.RespondAsync($"No players from the `[{clan.ClanTag}]` has battles on the `{tank.Name}`, {ctx.User.Mention}, as far as the database is up to date.");
                return;
            }

            var waitMsg = await ctx.RespondAsync($"Please wait as data for {players.Count} tankers is being retrieved, {ctx.User.Mention}, it may take a while...");

            var playerCommands = new PlayerCommands();

            var fullPlayers = new ConcurrentBag<Player>();
            var tasks = players.Select(async p => 
            {
                var player = await playerCommands.GetPlayer(ctx, ((p.Platform == Platform.XBOX) ? "x." : "ps.") + p.Name, false);
                if (player == null)
                {
                    await ctx.RespondAsync($"Sorry, could not get updated information for player `{p.Name}`, {ctx.User.Mention}.");
                    return;
                }

                fullPlayers.Add(player);
            });
            await Task.WhenAll(tasks);

            await waitMsg.DeleteAsync();

            var sb = new StringBuilder();

            var maxNameLength = fullPlayers.Max(p => p.Name.Length);

            //sb.AppendLine($"Here `[{clan.ClanTag}]` top players on the `{tank.Name}`, {ctx.User.Mention}:");
            //sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine($"{"Tanker".PadRight(maxNameLength)} {"Days",5} {"Battles",7} {"WN8",6}");
            foreach (var p in fullPlayers.OrderByDescending(p => p.Performance.All[tank.TankId].Wn8).Take(25))
            {
                var tp = p.Performance.All[tank.TankId];
                sb.AppendLine($"{(p.Name ?? string.Empty).PadRight(maxNameLength)} {(DateTime.UtcNow - tp.LastBattle).TotalDays,5:N0} {tp.Battles,7:N0} {tp.Wn8,6:N0}");
            }
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("This command is a **Premium** feature on the bot. For now it's free to use this command, but be advised that on the near future access will be restricted to Premium subscribers.");

            var color = clan.Top15Wn8.ToColor();
            
            var embed = new DiscordEmbedBuilder
            {
                Title = $"`{clan.ClanTag}` top players on the `{tank.Name}`",
                Description = sb.ToString(),
                Color = new DiscordColor(color.R, color.G, color.B),
                ThumbnailUrl = tank.SmallImageUrl,
                Url = $"https://wotclans.com.br/Clan/{clan.ClanTag}",
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    Name = "WoTClans",
                    Url = $"https://wotclans.com.br"
                },
                Footer = new DiscordEmbedBuilder.EmbedFooter
                {
                    Text = $"Calculated at {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC"
                }
            };

            await ctx.RespondAsync("", embed: embed);
        }

        [Command("clan")]
        [Description("A quick overview of a clan")]
        public async Task Clan(CommandContext ctx,
            [Description("The clan **tag**")] string clanTag,
            [Description("Put `true` to dump all members of the clan")]
            bool all = false,
            [Description("Put `true` to dump a bare list, with only the players names")]
            bool bare = false)
        {
            if (!await CanExecute(ctx, Features.Clans))
            {
                return;
            }

            await ctx.TriggerTypingAsync();

            if (string.IsNullOrWhiteSpace(clanTag))
            {
                await ctx.RespondAsync($"You must send a clan tag as parameter, {ctx.User.Mention}.");
                return;
            }

            Log.Debug($"Requesting {nameof(Clan)}({clanTag}, {all})...");

            var cfg = GuildConfiguration.FromGuild(ctx.Guild);
            var platform = GetPlatform(clanTag, cfg.Plataform, out clanTag);

            clanTag = clanTag.Trim('[', ']');
            clanTag = clanTag.ToUpperInvariant();

            if (!ClanTagRegex.IsMatch(clanTag))
            {
                await ctx.RespondAsync($"You must send a **valid** clan **tag** as parameter, {ctx.User.Mention}.");
                return;
            }

            var provider = new DbProvider(_connectionString);

            var clan = provider.GetClan(clanTag);
            if (clan == null)
            {
                platform = platform == Platform.PS ? Platform.XBOX : Platform.PS;

                clan = provider.GetClan(clanTag);
                if (clan == null)
                {
                    await ctx.RespondAsync(
                        $"Can't find on a clan with tag `[{clanTag}]`, {ctx.User.Mention}. Maybe my site doesn't track it yet... or you have the wrong clan tag.");
                    return;
                }
            }

            var platformPrefix = clan.Platform == Platform.PS ? "ps." : string.Empty;

            var sb = new StringBuilder();

            if (bare)
            {
                if (!clan.Enabled)
                {
                    sb.AppendLine($"Data collection for `{clan.ClanTag}` is **disabled**, the reason for this is {clan.DisabledReason}.");
                    await ctx.RespondAsync(sb.ToString());
                    return;
                }

                var players = all
                    ? clan.Players.OrderBy(p => p.Name).Select(p => p.Name).ToList()
                    : clan.Top15Players.Select(p => p.Name).ToList();

                sb.Append($"{(all ? $"All {players.Count}" : "Top 15")} Tankers on `{clan.ClanTag}`");
                if (!string.IsNullOrWhiteSpace(clan.Country))
                {
                    sb.Append($" ({clan.Country.ToUpperInvariant()})");
                }
                sb.AppendLine($", on the {clan.Platform}, {ctx.User.Mention}:");
                
                sb.AppendLine("```");

                
                    

                foreach (var gamerTag in players)
                {
                    sb.AppendLine(gamerTag);
                }
                sb.AppendLine("```");
                await ctx.RespondAsync(sb.ToString());
            }
            else
            {
                sb.Append($"Information about `{clan.ClanTag}`");
                if (!string.IsNullOrWhiteSpace(clan.Country))
                {
                    sb.Append($" ({clan.Country.ToUpperInvariant()})");
                }
                sb.AppendLine($", on the {clan.Platform}, {ctx.User.Mention}:");

                if (!clan.Enabled)
                {
                    sb.AppendLine(
                        $"Data collection for this clan is **disabled**, the reason for this is {clan.DisabledReason}.");
                }

                sb.AppendLine();

                sb.AppendLine($"Active Members: {clan.Active}; Total Members: {clan.Count};");
                sb.AppendLine($"Recent Win Rate: {clan.ActiveWinRate:P1}; Overall Win Rate: {clan.TotalWinRate:P1};");
                sb.AppendLine($"Recent WN8t15: {clan.Top15Wn8:N0}; Overall WN8: {clan.TotalWn8:N0};");
                if (clan.DeltaDayTop15Wn8.HasValue)
                {
                    sb.AppendLine($"WN8t15 Variation from 1 day ago: {clan.DeltaDayTop15Wn8.Value:N0}");
                }

                if (clan.DeltaWeekTop15Wn8.HasValue)
                {
                    sb.AppendLine($"WN8t15 Variation from 1 week ago: {clan.DeltaWeekTop15Wn8.Value:N0}");
                }

                if (clan.DeltaMonthTop15Wn8.HasValue)
                {
                    sb.AppendLine($"WN8t15 Variation from 1 month ago: {clan.DeltaMonthTop15Wn8.Value:N0}");
                }

                sb.AppendLine(
                    $"Recent Actives Battles: {clan.ActiveBattles:N0}; Recent Avg Tier: {clan.ActiveAvgTier:N1}");

                sb.AppendLine();
                sb.AppendLine("**Top 15 Active Players**");
                foreach (var p in clan.Top15Players)
                {
                    sb.AppendLine(
                        $"{Formatter.MaskedUrl(p.Name, new Uri(p.PlayerOverallUrl))}, {p.MonthBattles} battles, WN8: {p.MonthWn8:N0}");
                }

                var title = clan.ClanTag.EqualsCiAi(clan.Name) ? clan.ClanTag : $"{clan.ClanTag} - {clan.Name}";

                var color = clan.Top15Wn8.ToColor();

                var embed = new DiscordEmbedBuilder
                {
                    Title = title,
                    Description = sb.ToString(),
                    Color = new DiscordColor(color.R, color.G, color.B),
                    Url = $"https://{platformPrefix}wotclans.com.br/Clan/{clan.ClanTag}",
                    Author = new DiscordEmbedBuilder.EmbedAuthor
                    {
                        Name = "WoTClans",
                        Url = $"https://{platform}wotclans.com.br"
                    },
                    Footer = new DiscordEmbedBuilder.EmbedFooter
                    {
                        Text = $"Data calculated at {clan.Moment:yyyy-MM-dd HH:mm} UTC."
                    }
                };

                Log.Debug($"Returned {nameof(Clan)}({clan.Platform}.{clan.ClanTag})");

                await ctx.RespondAsync("", embed: embed);
            }


            if (all & !bare)
            {
                // We need more responses, as discord limits the amount of data we can send on one message
                
                var allPlayers = clan.Players.OrderByDescending(p => p.MonthBattles).ThenByDescending(p => p.TotalBattles).ToArray();
                const int pageSize = 15;
                var pages = allPlayers.Length / pageSize + 1;

                var color = clan.TotalWn8.ToColor();

                for (var currentPage = 0; currentPage < pages; ++currentPage)
                {
                    sb.Clear();

                    var title = (clan.ClanTag.EqualsCiAi(clan.Name) ? clan.ClanTag : $"{clan.ClanTag} - {clan.Name}") +
                            $" - Page {currentPage + 1} of {pages}";

                    sb.Append($"All members of the `{clan.ClanTag}`");
                    if (!string.IsNullOrWhiteSpace(clan.Country))
                    {
                        sb.Append($" ({clan.Country.ToUpperInvariant()})");
                    }

                    sb.AppendLine($", on the {clan.Platform}:");
                    sb.AppendLine();

                    foreach (var p in allPlayers.Skip(currentPage*pageSize).Take(pageSize))
                    {
                        sb.AppendLine($"{Formatter.MaskedUrl(p.Name, new Uri(p.PlayerOverallUrl))}, {p.MonthBattles} battles, WN8: {p.MonthWn8:N0}");
                    }

                    var embed = new DiscordEmbedBuilder
                    {
                        Title = title,
                        Description = sb.ToString(),
                        Color = new DiscordColor(color.R, color.G, color.B),
                        Url = $"https://{platformPrefix}wotclans.com.br/Clan/{clan.ClanTag}",
                        Author = new DiscordEmbedBuilder.EmbedAuthor
                        {
                            Name = "WoTClans",
                            Url = $"https://{platform}wotclans.com.br"
                        },
                        Footer = new DiscordEmbedBuilder.EmbedFooter
                        {
                            Text = $"Page {currentPage + 1} of {pages}"
                        }
                    };

                    await ctx.RespondAsync("", embed: embed);

                }
                               
            }

        }
    }
}