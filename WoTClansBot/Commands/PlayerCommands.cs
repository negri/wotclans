using System;
using System.Configuration;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using log4net;
using Negri.Wot.Sql;
using Negri.Wot.Tanks;
using Negri.Wot.WgApi;

namespace Negri.Wot.Bot
{
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    internal class PlayerCommands : CommandsBase
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(PlayerCommands));

        private readonly string _connectionString;

        public PlayerCommands()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;
        }

        /// <summary>
        ///     Retrieves a player from database, or only WG API
        /// </summary>
        public async Task<Player> GetPlayer(CommandContext ctx, string gamerTag)
        {
            try
            {
                var cfg = GuildConfiguration.FromGuild(ctx.Guild);
                var platform = GetPlatform(gamerTag, cfg.Plataform, out gamerTag);

                var provider = new DbProvider(_connectionString);
                var recorder = new DbRecorder(_connectionString);

                DiscordMessage willTryApiMessage = null;

                long? playerId = null;
                if (gamerTag.EqualsCiAi("me"))
                {
                    playerId = provider.GetPlayerIdByDiscordId((long) ctx.User.Id);
                }

                playerId = playerId ?? provider.GetPlayerIdByName(platform, gamerTag);
                if (playerId == null)
                {
                    Log.Debug($"Could not find player {platform}.{gamerTag} on the database... trying the API...");
                    willTryApiMessage = await ctx.RespondAsync($"I could not find a player on `{platform}` " +
                                                               $"with the Gamer Tag `{gamerTag}` on the Database, {ctx.User.Mention}. I will try the Wargaming API... it may take some time...");
                }

                var cacheDirectory = ConfigurationManager.AppSettings["CacheDir"] ?? Path.GetTempPath();
                var webCacheAge = TimeSpan.FromHours(4);
                var appId = ConfigurationManager.AppSettings["WgAppId"] ?? "demo";

                var fetcher = new Fetcher(cacheDirectory)
                {
                    ApplicationId = appId,
                    WebCacheAge = webCacheAge,
                    WebFetchInterval = TimeSpan.FromSeconds(1)
                };

                Player apiPlayer = null;
                if (playerId == null)
                {
                    await ctx.TriggerTypingAsync();

                    apiPlayer = fetcher.GetPlayerByGamerTag(platform, gamerTag);
                    if (apiPlayer == null)
                    {
                        Log.Debug($"Could not find player {platform}.{gamerTag} on the WG API.");

                        if (willTryApiMessage != null)
                        {
                            await willTryApiMessage.DeleteAsync("Information updated.");
                        }

                        await ctx.RespondAsync(
                            $"Sorry, {ctx.User.Mention}. I could not find a player on `{platform}` " +
                            $"with the Gamer Tag `{gamerTag}` on the Wargaming API. Are you sure about the **exact** gamer tag?.");
                        return null;
                    }

                    playerId = apiPlayer.Id;
                }

                var player = provider.GetPlayer(playerId.Value, true);

                var wn8Expected = provider.GetWn8ExpectedValues(player?.Plataform ?? apiPlayer?.Plataform ?? Plataform.XBOX);

                if (player == null && apiPlayer != null)
                {
                    // Not on my database, but on the API Let's work with overall data!
                    var tanks = fetcher.GetTanksForPlayer(apiPlayer.Plataform, apiPlayer.Id).ToArray();
                    foreach (var t in tanks)
                    {
                        t.All.TreesCut = t.TreesCut;
                        t.All.BattleLifeTimeSeconds = t.BattleLifeTimeSeconds;
                        t.All.MarkOfMastery = t.MarkOfMastery;
                        t.All.MaxFrags = t.MaxFrags;
                        t.All.LastBattle = t.LastBattle;
                    }

                    apiPlayer.Performance = new TankPlayerPeriods
                    {
                        All = tanks.ToDictionary(t => t.TankId, t => t.All)
                    };

                    apiPlayer.Calculate(wn8Expected);

                    if (willTryApiMessage != null)
                    {
                        await willTryApiMessage.DeleteAsync("Information updated.");
                    }

                    return apiPlayer;
                }

                if (player.Age.TotalHours > 4)
                {
                    willTryApiMessage = await ctx.RespondAsync($"Data for  `{player.Name}` on `{player.Plataform}` " +
                                                               $"is more than {player.Age.TotalHours:N0}h old, {ctx.User.Mention}. Retrieving fresh data, please wait...");

                    var tanks = fetcher.GetTanksForPlayer(player.Plataform, player.Id);
                    var allTanks = provider.GetTanks(player.Plataform).ToDictionary(t => t.TankId);
                    var validTanks = tanks.Where(t => allTanks.ContainsKey(t.TankId)).ToArray();
                    recorder.Set(validTanks);

                    var played = provider.GetWn8RawStatsForPlayer(player.Plataform, player.Id);
                    player.Performance = played;
                    player.Calculate(wn8Expected);
                    player.Moment = DateTime.UtcNow;
                    player.Origin = PlayerDataOrigin.Self;

                    var previous = provider.GetPlayer(player.Id, player.Date, true);
                    if (previous != null)
                    {
                        if (player.Check(previous, true))
                        {
                            Log.Warn($"Player {player.Name}.{player.Id}@{player.Plataform} was patched.");
                        }
                    }

                    if (player.CanSave())
                    {
                        recorder.Set(player);
                        if (!player.IsPatched)
                        {
                            var putter = new Putter(player.Plataform, ConfigurationManager.AppSettings["ApiAdminKey"]);
                            putter.Put(player);
                        }
                    }
                    else
                    {
                        Log.Warn($"Player {player.Name}.{player.Id}@{player.Plataform} has to much zero data.");
                    }
                }
                else
                {
                    player.Calculate(wn8Expected);
                }

                if (willTryApiMessage != null)
                {
                    await willTryApiMessage.DeleteAsync("Information updated.");
                }

                return player;
            }
            catch (Exception ex)
            {
                Log.Error($"{nameof(GetPlayer)}({gamerTag})", ex);
                await ctx.RespondAsync(
                    $"Sorry, {ctx.User.Mention}. There was an error... the *Coder* will be notified of `{ex.Message}`.");
                return null;
            }
        }

        [Command("TankerTop")]
        [Description("The top tanks of a player")]
        public async Task TankerTop(CommandContext ctx,
            [Description("The *gamer tag* or *PSN Name*")]
            string gamerTag,
            [Description("Minimum tier")] int minTier = 5,
            [Description("Maximum tier")] int maxTier = 10,
            [Description("Include premiums tanks. Use *true* or *false*.")]
            bool includePremiums = false)
        {
            if (!await CanExecute(ctx, Features.Players))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(gamerTag))
            {
                await ctx.RespondAsync(
                    $"Please specify the *Gamer Tag*, {ctx.User.Mention}. Something like `!w tanker {ctx.User.Username.RemoveDiacritics()}`, for example.");
                return;
            }

            Log.Debug($"Requesting {nameof(TankerTop)}({gamerTag}, {minTier}, {maxTier}, {includePremiums})...");

            try
            {
                var player = await GetPlayer(ctx, gamerTag);
                if (player == null)
                {
                    Log.Debug($"Not found player '{gamerTag}'.");
                    return;
                }

                var top = player.Performance.GetTopTanks(ReferencePeriod.All, 25, minTier, maxTier, includePremiums)
                    .ToArray();
                if (!top.Any())
                {
                    await ctx.RespondAsync(
                        $"Sorry, {ctx.User.Mention}. There are not enough tanks with the specified filters.");
                }

                var sb = new StringBuilder();
                if (string.IsNullOrWhiteSpace(player.ClanTag))
                {
                    sb.AppendLine($"*{player.Name}* Top Tanks, {ctx.User.Mention}:");
                }
                else
                {
                    sb.AppendLine($"*{player.Name}* [{Formatter.MaskedUrl(player.ClanTag, new Uri(player.ClanUrl))}] " +
                                  $"Top Tanks, {ctx.User.Mention}:");
                }

                sb.AppendLine();
                sb.AppendLine("```");
                sb.AppendLine("Tank            Battles  WN8");
                foreach (var t in top)
                {
                    sb.AppendLine(
                        $"{t.Name.PadRight(15)} {t.Battles.ToString("N0").PadLeft(7)} {t.Wn8.ToString("N0").PadLeft(6)}");
                }

                sb.AppendLine("```");

                var color = top.First().Wn8.ToColor();
                var platformPrefix = player.Plataform == Plataform.PS ? "ps." : string.Empty;

                var embed = new DiscordEmbedBuilder
                {
                    Title = player.Name,
                    Description = sb.ToString(),
                    Color = new DiscordColor(color.R, color.G, color.B),
                    Url = player.PlayerOverallUrl,
                    Author = new DiscordEmbedBuilder.EmbedAuthor
                    {
                        Name = "WoTClans",
                        Url = $"https://{platformPrefix}wotclans.com.br"
                    },
                    Footer = new DiscordEmbedBuilder.EmbedFooter
                    {
                        Text = $"Calculated at {player.Moment:yyyy-MM-dd HH:mm} UTC."
                    }
                };

                await ctx.RespondAsync("", embed: embed);
            }
            catch (Exception ex)
            {
                Log.Error($"{nameof(TankerTop)}({gamerTag}, {minTier}, {maxTier}, {includePremiums})", ex);
                await ctx.RespondAsync(
                    $"Sorry, {ctx.User.Mention}. There was an error... the *Coder* will be notified of `{ex.Message}`.");
            }
        }

        [Command("tanker")]
        [Aliases("player", "gamer")]
        [Description("A quick overview of a player")]
        public async Task Tanker(CommandContext ctx, [Description("The *gamer tag* or *PSN Name*")] [RemainingText]
            string gamerTag)
        {
            if (!await CanExecute(ctx, Features.Players))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(gamerTag))
            {
                await ctx.RespondAsync(
                    $"Please specify the *Gamer Tag*, {ctx.User.Mention}. Something like `!w tanker {ctx.User.Username.RemoveDiacritics()}`, for example.");
                return;
            }

            await ctx.TriggerTypingAsync();

            Log.Debug($"Requesting {nameof(Tanker)}({gamerTag})...");

            try
            {
                var player = await GetPlayer(ctx, gamerTag);
                if (player == null)
                {
                    return;
                }

                var maxTier = player.Performance.All.Values.Max(t => t.Tier);
                var lastBattle = player.Performance.All.Values.Max(t => t.LastBattle);

                var color = player.TotalWn8.ToColor();
                var platformPrefix = player.Plataform == Plataform.PS ? "ps." : string.Empty;

                var sb = new StringBuilder();
                if (string.IsNullOrWhiteSpace(player.ClanTag))
                {
                    sb.AppendLine($"Information about the tanker, {ctx.User.Mention}:");
                }
                else
                {
                    sb.AppendLine(
                        $"Information about the [{Formatter.MaskedUrl(player.ClanTag, new Uri(player.ClanUrl))}] " +
                        $"tanker, {ctx.User.Mention}:");
                }

                sb.AppendLine();
                sb.AppendLine(
                    $"Last Battle: {lastBattle:yyyy-MM-dd HH:mm} UTC, {(DateTime.UtcNow - lastBattle).TotalDays:N0} days ago.");
                sb.AppendLine($"Battles: {player.TotalBattles:N0}; Hours battling: {player.TotalTime.TotalHours:N0}");
                if (player.MonthBattles > 0)
                {
                    sb.AppendLine(
                        $"Last Month Battles: {player.MonthBattles:N0}; Last Month Hours Battling: {player.MonthTime.TotalHours:N0}");
                }

                sb.Append($"Maximum Tier: {maxTier}; Avg Tier: {player.TotalTier:N1}");
                if (player.MonthBattles > 0)
                {
                    sb.Append($"; Last Month Avg Tier: {player.MonthTier:N1}");
                }

                sb.AppendLine();
                sb.AppendLine($"Win Rate: {player.TotalWinRate:P1}; WN8: {player.TotalWn8:N0}");
                if (player.MonthBattles > 0)
                {
                    sb.AppendLine(
                        $"Last Month Win Rate: {player.MonthWinRate:P1}; Last Month WN8: {player.MonthWn8:N0}");
                }

                sb.AppendLine();

                var rs = player.Performance.All.Values.OrderByDescending(t => t.LastBattle).Take(5).ToArray();
                sb.AppendLine($"**Last {rs.Length} Played Tanks**");
                foreach (var t in rs)
                {
                    sb.AppendLine(
                        $"{Formatter.MaskedUrl(t.Name, new Uri($"https://{platformPrefix}wotclans.com.br/Tanks/{t.TankId}"))}: {t.Battles:N0} battles, " +
                        $"WN8: {t.Wn8:N0}, " +
                        $"Dmg: {t.TotalDamagePerBattle:N0};");
                }

                sb.AppendLine();

                var ts = player.Performance.GetTopTanks(ReferencePeriod.All, 25, 5).Take(5).ToArray();
                if (ts.Any())
                {
                    sb.AppendLine("**Top 5 Tanks**");
                    foreach (var t in ts)
                    {
                        sb.AppendLine(
                            $"{Formatter.MaskedUrl(t.Name, new Uri($"https://{platformPrefix}wotclans.com.br/Tanks/{t.TankId}"))}: {t.Battles:N0} battles, " +
                            $"WN8: {t.Wn8:N0}, " +
                            $"Dmg: {t.TotalDamagePerBattle:N0};");
                    }
                }

                var tms = player.Performance.GetTopTanks(ReferencePeriod.Month).ToArray();
                if (tms.Any())
                {
                    sb.AppendLine();
                    sb.AppendLine("**Top 5 Last Month Tanks**");
                    foreach (var t in tms)
                    {
                        sb.AppendLine(
                            $"{Formatter.MaskedUrl(t.Name, new Uri($"https://{platformPrefix}wotclans.com.br/Tanks/{t.TankId}"))}: {t.Battles:N0} battles, " +
                            $"WN8: {t.Wn8:N0}, " +
                            $"Dmg: {t.TotalDamagePerBattle:N0};");
                    }
                }

                var embed = new DiscordEmbedBuilder
                {
                    Title = player.Name,
                    Description = sb.ToString(),
                    Color = new DiscordColor(color.R, color.G, color.B),
                    Url = player.PlayerOverallUrl,
                    Author = new DiscordEmbedBuilder.EmbedAuthor
                    {
                        Name = "WoTClans",
                        Url = $"https://{platformPrefix}wotclans.com.br"
                    },
                    Footer = new DiscordEmbedBuilder.EmbedFooter
                    {
                        Text = $"Calculated at {player.Moment:yyyy-MM-dd HH:mm} UTC."
                    }
                };

                var bestTank = player.Performance.GetBestTank();
                if (bestTank != null)
                {
                    embed.ThumbnailUrl = bestTank.SmallImageUrl;
                }

                await ctx.RespondAsync("", embed: embed);
            }
            catch (Exception ex)
            {
                Log.Error($"{nameof(Tanker)}({gamerTag})", ex);
                await ctx.RespondAsync(
                    $"Sorry, {ctx.User.Mention}. There was an error... the *Coder* will be notified of `{ex.Message}`.");
            }
        }

        [Command("WhoIAm")]
        [Description("Returns the Gamer Tag that is associated with your Discord user")]
        public async Task WhoIAm(CommandContext ctx)
        {
            try
            {
                await ctx.TriggerTypingAsync();

                if (!await CanExecute(ctx, Features.Players))
                {
                    return;
                }

                var userId = (long) (ctx.User?.Id ?? 0UL);
                if (userId == 0)
                {
                    await ctx.RespondAsync($"Sorry, {ctx.User?.Mention}. I don't now your Discord User Id! WTF!??!");
                    return;
                }

                var provider = new DbProvider(_connectionString);
                var playerId = provider.GetPlayerIdByDiscordId(userId);
                if (playerId == null)
                {
                    await ctx.RespondAsync(
                        $"Sorry, {ctx.User?.Mention}, I have no idea who you are. You can change this by using the command `SetWhoIAm`.");
                    return;
                }

                var player = provider.GetPlayer(playerId.Value);
                Debug.Assert(player != null);

                await ctx.RespondAsync(
                    $"{ctx.User?.Mention}, as far as I know your {(player.Plataform == Plataform.PS ? "PSN Name" : "Gamer Tag")} is `{player.Name}`.");
            }
            catch (Exception ex)
            {
                Log.Error($"{nameof(WhoIAm)}()", ex);
                await ctx.RespondAsync(
                    $"Sorry, {ctx.User?.Mention}. There was an error... the *Coder* will be notified of `{ex.Message}`.");
            }
        }

        [Command("SetWhoIAm")]
        [Description(
            "Associates your Discord user with a Gamer Tag, so you can user *me* when referring to yourself on commands.")]
        public async Task SetWhoIAm(CommandContext ctx,
            [Description("The *gamer tag* or *PSN Name*")] [RemainingText]
            string gamerTag = "")
        {
            try
            {
                await ctx.TriggerTypingAsync();

                if (!await CanExecute(ctx, Features.Players))
                {
                    return;
                }

                var userId = (long) (ctx.User?.Id ?? 0UL);
                if (userId == 0)
                {
                    await ctx.RespondAsync($"Sorry, {ctx.User?.Mention}. I don't now your Discord User Id! WTF!??!");
                    return;
                }

                var cfg = GuildConfiguration.FromGuild(ctx.Guild);
                var platform = GetPlatform(gamerTag, cfg.Plataform, out gamerTag);

                if (string.IsNullOrWhiteSpace(gamerTag))
                {
                    gamerTag = ctx.User?.Username ?? string.Empty;
                }

                var provider = new DbProvider(_connectionString);

                var playerId = provider.GetPlayerIdByName(platform, gamerTag);
                if (playerId == null)
                {
                    await ctx.RespondAsync(
                        $"Sorry, {ctx.User?.Mention}, I do not track `{gamerTag}` yet. Are you a member of a tracked clan? Are you sure about the gamer tag?");
                    return;
                }

                var recorder = new DbRecorder(_connectionString);
                recorder.AssociateDiscordUserToPlayer(userId, playerId.Value);

                await ctx.RespondAsync(
                    $"Ok, {ctx.User?.Mention}, for now on you can use `me` instead of your full {platform.TagName()} on " +
                    "commands that take it as a parameters. I promise to never abuse this association, and protected it from use outside of this system. " +
                    "If you want me to remove this piece of information use the `ForgetWhoIAm` command.");
            }
            catch (Exception ex)
            {
                Log.Error($"{nameof(WhoIAm)}()", ex);
                await ctx.RespondAsync(
                    $"Sorry, {ctx.User?.Mention}. There was an error... the *Coder* will be notified of `{ex.Message}`.");
            }
        }

        [Command("ForgetWhoIAm")]
        [Description("Removes the association between your Discord user and your Gamer Tag.")]
        public async Task ForgetWhoIAm(CommandContext ctx)
        {
            try
            {
                await ctx.TriggerTypingAsync();

                if (!await CanExecute(ctx, Features.Players))
                {
                    return;
                }

                var userId = (long) (ctx.User?.Id ?? 0UL);
                if (userId == 0)
                {
                    await ctx.RespondAsync($"Sorry, {ctx.User?.Mention}. I don't now your Discord User Id! WTF!??!");
                    return;
                }

                var recorder = new DbRecorder(_connectionString);
                recorder.AssociateDiscordUserToPlayer(userId, 0);

                await ctx.RespondAsync($"Done, {ctx.User?.Mention}, I no longer know who you are on the game.");
            }
            catch (Exception ex)
            {
                Log.Error($"{nameof(WhoIAm)}()", ex);
                await ctx.RespondAsync(
                    $"Sorry, {ctx.User?.Mention}. There was an error... the *Coder* will be notified of `{ex.Message}`.");
            }
        }

        [Command("TankerXP")]
        [Description("The top XP earners of a player")]
        public async Task TankerXP(CommandContext ctx,
            [Description("The *gamer tag* or *PSN Name*")]
            string gamerTag,
            [Description("Minimum Battles")] int minBattles = 50,
            [Description("Minimum tier")] int minTier = 5,
            [Description("Maximum tier")] int maxTier = 10,
            [Description("Show only premiums tanks. Use *true* or *false*.")] bool onlyPremium = false,
            [Description("The tank nation. Use the `nations` command to see the valid values, or `any`")] string nation = null,
            [Description("Sort by XP/hour instead of the default XP/battle.")] bool sortByHour = false)
        {
            if (!await CanExecute(ctx, Features.Players))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(gamerTag))
            {
                await ctx.RespondAsync(
                    $"Please specify the *Gamer Tag*, {ctx.User.Mention}. Something like `!w tanker {ctx.User.Username.RemoveDiacritics()}`, for example.");
                return;
            }

            Log.Debug(
                $"Requesting {nameof(TankerXP)}({gamerTag}, {minBattles}, {minTier}, {maxTier}, {onlyPremium})...");

            try
            {
                var player = await GetPlayer(ctx, gamerTag);
                if (player == null)
                {
                    Log.Debug($"Not found player '{gamerTag}'.");
                    return;
                }

                Nation? nationValue = null;
                if (!string.IsNullOrWhiteSpace(nation))
                {
                    if (string.Equals(nation, "any", StringComparison.InvariantCultureIgnoreCase))
                    {
                        // They don't care
                    }
                    else if (Enum.TryParse(nation, true, out Nation n))
                    {
                        nationValue = n;
                    }
                    else
                    {
                        Log.Debug($"Invalid nation: {nation}.");

                        await ctx.RespondAsync(
                            $"Sorry, {ctx.User.Mention}. The supplied nation parameters does not correspond to something I can use. Try the `nations` command to see the valid values.");

                        return;                        
                    }
                }

                var isPremium = onlyPremium ? true : (bool?) null;
                var tanks = player.Performance
                    .GetTanks(ReferencePeriod.All, minTier, maxTier, isPremium, null, minBattles, nationValue)
                    .OrderByDescending(t => sortByHour ? t.XPPerHour : t.XPPerBattle).Take(25).ToArray();
                if (!tanks.Any())
                {
                    await ctx.RespondAsync(
                        $"Sorry, {ctx.User.Mention}. There are not enough tanks with the specified filters.");
                    return;
                }

                var sb = new StringBuilder();
                if (string.IsNullOrWhiteSpace(player.ClanTag))
                {
                    sb.AppendLine($"*{player.Name}* Top XP Earners, {ctx.User.Mention}:");
                }
                else
                {
                    sb.AppendLine($"*{player.Name}* [{Formatter.MaskedUrl(player.ClanTag, new Uri(player.ClanUrl))}] " +
                                  $"Top XP Earners, {ctx.User.Mention}:");
                }

                sb.AppendLine();
                sb.AppendLine("```");
                sb.AppendLine("Tank            Battles     XP    XP/h");
                foreach (var t in tanks)
                {
                    sb.AppendLine(
                        $"{t.Name.PadRight(15)} {t.Battles.ToString("N0").PadLeft(7)} {t.XPPerBattle.ToString("N0").PadLeft(6)}  {t.XPPerHour.ToString("N0").PadLeft(6)}");
                }

                sb.AppendLine("```");
                sb.AppendLine();
                sb.AppendLine(
                    "**Caution!** The Wargaming API accumulates the XP on every tank including the Premium Time, so these values are only reliable " +
                    "if you *always* plays with Premium, or *always* plays *without* Premium. If you mix, take these number with great reserve.");

                var color = tanks.First().Wn8.ToColor();
                var platformPrefix = player.Plataform == Plataform.PS ? "ps." : string.Empty;

                var embed = new DiscordEmbedBuilder
                {
                    Title = player.Name,
                    Description = sb.ToString(),
                    Color = new DiscordColor(color.R, color.G, color.B),
                    Url = player.PlayerOverallUrl,
                    Author = new DiscordEmbedBuilder.EmbedAuthor
                    {
                        Name = "WoTClans",
                        Url = $"https://{platformPrefix}wotclans.com.br"
                    },
                    Footer = new DiscordEmbedBuilder.EmbedFooter
                    {
                        Text = $"Calculated at {player.Moment:yyyy-MM-dd HH:mm} UTC."
                    }
                };

                await ctx.RespondAsync("", embed: embed);
            }
            catch (Exception ex)
            {
                Log.Error($"{nameof(TankerTop)}({gamerTag}, {minTier}, {maxTier}, {onlyPremium})", ex);
                await ctx.RespondAsync(
                    $"Sorry, {ctx.User.Mention}. There was an error... the *Coder* will be notified of `{ex.Message}`.");
            }
        }
    }
}