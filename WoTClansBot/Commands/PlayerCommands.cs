using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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
        public async Task<Player> GetPlayer(CommandContext ctx, string gamerTag, bool showStatus = true)
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
                    playerId = provider.GetPlayerIdByDiscordId((long)ctx.User.Id);
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

                var player = provider.GetPlayer(playerId.Value, true, true);

                var wn8Expected = provider.GetWn8ExpectedValues(player?.Plataform ?? apiPlayer?.Plataform ?? Platform.XBOX);

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

                if (player == null)
                {
                    Log.Warn($"{nameof(GetPlayer)}({gamerTag}) has no history on database or Wargaming. Does he ever played the game?");
                    await ctx.RespondAsync(
                        $"Sorry, {ctx.User.Mention}. There is no history on database or Wargaming for the player `{gamerTag}`. Does he ever played the game?");
                    return null;
                }

                if (player.Age.TotalHours > 1)
                {
                    if (showStatus)
                    {
                        willTryApiMessage = await ctx.RespondAsync($"Data for  `{player.Name}` on `{player.Plataform}` " +
                                                                   $"is more than {player.Age.TotalHours:N0}h old, {ctx.User.Mention}. Retrieving fresh data, please wait...");
                    }

                    var tanks = fetcher.GetTanksForPlayer(player.Plataform, player.Id);
                    var allTanks = provider.GetTanks(player.Plataform).ToDictionary(t => t.TankId);
                    var validTanks = tanks.Where(t => allTanks.ContainsKey(t.TankId)).ToArray();
                    recorder.Set(validTanks);

                    var played = provider.GetWn8RawStatsForPlayer(player.Plataform, player.Id, true);
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

                            _ =Task.Run(() =>
                              {
                                  try
                                  {
                                      var putter = new Putter(player.Plataform, ConfigurationManager.AppSettings["ApiAdminKey"]);
                                      putter.Put(player);
                                  }
                                  catch (Exception ex)
                                  {
                                      Log.Error($"Error putting player {player.Id} on the remote site.", ex);
                                  }
                              });

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

        [Command("TankerMedalRate")]
        [Aliases("MedalRate", "GamerMedalRate")]
        [Description("The rate at witch a tanker gets a particular medal with his tanks")]
        public async Task TankerMedalRate(CommandContext ctx,
            [Description("The *gamer tag* or *PSN Name*")] string gamerTag,
            [Description("The medal name")] string medal,
            [Description("The minimum number of battles on the tank")] int minBattles = 1,
            [Description("The minimum tier of the tanks")] int minTier = 1,
            [Description("Nation of the tank, or *any*. Multiple values can be sent using *;* as separators")] string nationFilter = "any",
            [Description("Type of the tank, or *any*. Multiple values can be sent using *;* as separators")] string typeFilter = "any")
        {
            if (!await CanExecute(ctx, Features.Players))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(gamerTag))
            {
                await ctx.RespondAsync(
                    $"Please specify the *Gamer Tag*, {ctx.User.Mention}. Something like `!w TankerHistory \"{ctx.User.Username.RemoveDiacritics()}\"`, for example.");
                return;
            }

            if (string.IsNullOrWhiteSpace(medal))
            {
                await ctx.RespondAsync(
                    $"Please specify the *medal*, {ctx.User.Mention}.");
                return;
            }

            Log.Debug($"Requesting {nameof(TankerMedalRate)}({gamerTag}, {medal}, {minBattles}, {minTier}, {nationFilter}, {typeFilter})...");

            try
            {
                await ctx.TriggerTypingAsync();

                var player = await GetPlayer(ctx, gamerTag);
                if (player == null)
                {
                    Log.Debug($"Not found player '{gamerTag}'.");
                    return;
                }

                #region Shortcuts for filters used on mercenaries contracts

                if (medal.EqualsCiAi("chisel5"))
                {
                    medal = "mainGun";
                    if (minTier < 8)
                    {
                        minTier = 8;
                    }
                    if (string.IsNullOrEmpty(nationFilter) || nationFilter.EqualsCiAi("any"))
                    {
                        nationFilter = "usa,uk,mercenaries";
                    }
                    if (string.IsNullOrEmpty(typeFilter) || typeFilter.EqualsCiAi("any"))
                    {
                        typeFilter = "medium,heavy";
                    }
                }
                else if (medal.EqualsCiAi("chisel6"))
                {
                    medal = "duelist";
                    if (minTier < 8)
                    {
                        minTier = 8;
                    }
                    if (string.IsNullOrEmpty(nationFilter) || nationFilter.EqualsCiAi("any"))
                    {
                        nationFilter = "usa,uk,mercenaries";
                    }
                    if (string.IsNullOrEmpty(typeFilter) || typeFilter.EqualsCiAi("any"))
                    {
                        typeFilter = "medium,heavy";
                    }
                }
                else if (medal.EqualsCiAi("cruncher6"))
                {
                    medal = "duelist";
                    if (minTier < 6)
                    {
                        minTier = 6;
                    }
                    if (string.IsNullOrEmpty(nationFilter) || nationFilter.EqualsCiAi("any"))
                    {
                        nationFilter = "germany,usa,czechoslovakia,mercenaries";
                    }
                    if (string.IsNullOrEmpty(typeFilter) || typeFilter.EqualsCiAi("any"))
                    {
                        typeFilter = "medium,heavy";
                    }
                }
                else if (medal.EqualsCiAi("jammer5"))
                {
                    medal = "mainGun";
                    if (minTier < 5)
                    {
                        minTier = 5;
                    }
                    if (string.IsNullOrEmpty(nationFilter) || nationFilter.EqualsCiAi("any"))
                    {
                        nationFilter = "usa,mercenaries";
                    }
                    if (string.IsNullOrEmpty(typeFilter) || typeFilter.EqualsCiAi("any"))
                    {
                        typeFilter = "medium,heavy";
                    }
                }
                else if (medal.EqualsCiAi("jammer6"))
                {
                    medal = "duelist";
                    if (minTier < 6)
                    {
                        minTier = 6;
                    }
                    if (string.IsNullOrEmpty(nationFilter) || nationFilter.EqualsCiAi("any"))
                    {
                        nationFilter = "ussr,usa,mercenaries";
                    }
                    if (string.IsNullOrEmpty(typeFilter) || typeFilter.EqualsCiAi("any"))
                    {
                        typeFilter = "medium,TankDestroyer";
                    }
                }
                else if (medal.EqualsCiAi("plaguebringer6demolition"))
                {
                    medal = "demolition";
                    if (minTier < 6)
                    {
                        minTier = 6;
                    }
                    if (string.IsNullOrEmpty(nationFilter) || nationFilter.EqualsCiAi("any"))
                    {
                        nationFilter = "usa,mercenaries";
                    }
                    if (string.IsNullOrEmpty(typeFilter) || typeFilter.EqualsCiAi("any"))
                    {
                        typeFilter = "medium,TankDestroyer";
                    }
                }
                else if (medal.EqualsCiAi("plaguebringer6arsonist"))
                {
                    medal = "demolition";
                    if (minTier < 6)
                    {
                        minTier = 6;
                    }
                    if (string.IsNullOrEmpty(nationFilter) || nationFilter.EqualsCiAi("any"))
                    {
                        nationFilter = "usa,mercenaries";
                    }
                    if (string.IsNullOrEmpty(typeFilter) || typeFilter.EqualsCiAi("any"))
                    {
                        typeFilter = "medium,TankDestroyer";
                    }
                }
                else if (medal.EqualsCiAi("stinger5"))
                {
                    medal = "confederate";
                    if (minTier < 7)
                    {
                        minTier = 7;
                    }
                    if (string.IsNullOrEmpty(nationFilter) || nationFilter.EqualsCiAi("any"))
                    {
                        nationFilter = "france,mercenaries";
                    }
                    if (string.IsNullOrEmpty(typeFilter) || typeFilter.EqualsCiAi("any"))
                    {
                        typeFilter = "Medium,Light";
                    }
                }
                else if (medal.EqualsCiAi("stinger6"))
                {
                    medal = "scout";
                    if (minTier < 5)
                    {
                        minTier = 5;
                    }
                    if (string.IsNullOrEmpty(nationFilter) || nationFilter.EqualsCiAi("any"))
                    {
                        nationFilter = "usa,france,mercenaries";
                    }
                    if (string.IsNullOrEmpty(typeFilter) || typeFilter.EqualsCiAi("any"))
                    {
                        typeFilter = "Light";
                    }
                }
                else if (medal.EqualsCiAi("longReach5"))
                {
                    medal = "bruiser";
                    if (minTier < 6)
                    {
                        minTier = 6;
                    }
                    if (string.IsNullOrEmpty(nationFilter) || nationFilter.EqualsCiAi("any"))
                    {
                        nationFilter = "germany,mercenaries";
                    }
                    if (string.IsNullOrEmpty(typeFilter) || typeFilter.EqualsCiAi("any"))
                    {
                        typeFilter = "Heavy";
                    }
                }
                else if (medal.EqualsCiAi("longReach6"))
                {
                    medal = "sniper";
                    if (minTier < 5)
                    {
                        minTier = 5;
                    }
                    if (string.IsNullOrEmpty(nationFilter) || nationFilter.EqualsCiAi("any"))
                    {
                        nationFilter = "uk,ussr,germany,mercenaries";
                    }
                    if (string.IsNullOrEmpty(typeFilter) || typeFilter.EqualsCiAi("any"))
                    {
                        typeFilter = "Heavy";
                    }
                }
                else if (medal.EqualsCiAi("roundabout6demolition"))
                {
                    medal = "demolition";
                    if (minTier < 4)
                    {
                        minTier = 4;
                    }
                    if (string.IsNullOrEmpty(nationFilter) || nationFilter.EqualsCiAi("any"))
                    {
                        nationFilter = "ussr,germany,usa,mercenaries";
                    }
                    if (string.IsNullOrEmpty(typeFilter) || typeFilter.EqualsCiAi("any"))
                    {
                        typeFilter = "light,TankDestroyer";
                    }
                }
                else if (medal.EqualsCiAi("roundabout6arsonist"))
                {
                    medal = "arsonist";
                    if (minTier < 4)
                    {
                        minTier = 4;
                    }
                    if (string.IsNullOrEmpty(nationFilter) || nationFilter.EqualsCiAi("any"))
                    {
                        nationFilter = "ussr,germany,usa,mercenaries";
                    }
                    if (string.IsNullOrEmpty(typeFilter) || typeFilter.EqualsCiAi("any"))
                    {
                        typeFilter = "light,TankDestroyer";
                    }
                }
                else if (medal.EqualsCiAi("lawgiver5"))
                {
                    medal = "mainGun";
                    if (minTier < 3)
                    {
                        minTier = 3;
                    }
                    if (string.IsNullOrEmpty(nationFilter) || nationFilter.EqualsCiAi("any"))
                    {
                        nationFilter = "germany,mercenaries";
                    }
                    if (string.IsNullOrEmpty(typeFilter) || typeFilter.EqualsCiAi("any"))
                    {
                        typeFilter = "Medium,Heavy,TankDestroyer";
                    }
                }
                else if (medal.EqualsCiAi("lawgiver6"))
                {
                    medal = "duelist";
                    if (minTier < 3)
                    {
                        minTier = 3;
                    }
                    if (string.IsNullOrEmpty(nationFilter) || nationFilter.EqualsCiAi("any"))
                    {
                        nationFilter = "germany,uk,mercenaries";
                    }
                    if (string.IsNullOrEmpty(typeFilter) || typeFilter.EqualsCiAi("any"))
                    {
                        typeFilter = "Medium,Light";
                    }
                }

                #endregion

                #region medal

                var provider = new DbProvider(_connectionString);
                var medals = provider.GetMedals(player.Plataform).Where(m => m.Category == Achievements.Category.Achievements).ToArray();

                var originalMedal = medal;
                medal = medal.RemoveDiacritics().ToLowerInvariant();

                var targetMedal = medals.FirstOrDefault(m => m.Code.EqualsCiAi(medal));
                if (targetMedal == null)
                {
                    targetMedal = medals.FirstOrDefault(m => m.Name.RemoveDiacritics().ToLowerInvariant().EqualsCiAi(medal));
                }
                if (targetMedal == null)
                {
                    targetMedal = medals.FirstOrDefault(m => m.Name.RemoveDiacritics().ToLowerInvariant().StartsWith(medal));
                }
                if (targetMedal == null)
                {
                    targetMedal = medals.FirstOrDefault(m => m.Name.RemoveDiacritics().ToLowerInvariant().Contains(medal));
                }
                if (targetMedal == null)
                {
                    medal = medal.GetFlatString();
                    targetMedal = medals.FirstOrDefault(m => m.Name.GetFlatString().EqualsCiAi(medal));
                }
                if (targetMedal == null)
                {
                    targetMedal = medals.FirstOrDefault(m => m.Name.GetFlatString().StartsWith(medal));
                }
                if (targetMedal == null)
                {
                    targetMedal = medals.FirstOrDefault(m => m.Name.GetFlatString().Contains(medal));
                }
                if (targetMedal == null)
                {
                    await ctx.RespondAsync(
                        $"Sorry, could not find a medal named **{originalMedal}**, {ctx.User.Mention}. " +
                        $"Check the game guide at https://console.worldoftanks.com/en/content/guide/achievements/ to see medal's names.");
                    return;
                }

                var tanksWithMedal = player.Performance.WithMedal(ReferencePeriod.All, targetMedal.Code).ToList();
                if (tanksWithMedal.Count <= 0)
                {
                    await ctx.RespondAsync(
                        $"Sorry, {ctx.User.Mention}, the player `{player.Name}` does not have any tank with the `{targetMedal.Name}` medal.");
                    return;
                }

                #endregion

                #region Filters

                tanksWithMedal = tanksWithMedal.Where(t => t.Tier >= minTier).ToList();
                tanksWithMedal = tanksWithMedal.Where(t => t.Battles >= minBattles).ToList();

                if (!string.IsNullOrWhiteSpace(nationFilter) && !nationFilter.EqualsCiAi("any"))
                {
                    var filtersText = nationFilter.Split(new[] { ',', ';', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var filters = new HashSet<Nation>();
                    foreach(var filterText in filtersText)
                    {
                        if (Enum.TryParse<Nation>(filterText, true, out var nation))
                        {
                            filters.Add(nation);
                        }
                        else
                        {
                            await ctx.RespondAsync(
                                $"Sorry, {ctx.User.Mention}, the nation `{filterText}` is not a valid nation. Valid nations are: {string.Join(", ", NationExtensions.GetGameNations().Select(n => $"`{n.ToString().ToLowerInvariant()}`"))}.");
                            return;
                        }
                    }

                    tanksWithMedal = tanksWithMedal.Where(t => filters.Contains(t.Nation)).ToList();
                }

                if (!string.IsNullOrWhiteSpace(typeFilter) && !typeFilter.EqualsCiAi("any"))
                {
                    var filtersText = typeFilter.Split(new[] { ',', ';', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var filters = new HashSet<TankType>();
                    foreach (var filterText in filtersText)
                    {
                        if (Enum.TryParse<TankType>(filterText, true, out var tankType))
                        {
                            filters.Add(tankType);
                        }
                        else
                        {
                            await ctx.RespondAsync(
                                $"Sorry, {ctx.User.Mention}, the tank type `{filterText}` is not a valid type. Valid tank types are: {string.Join(", ", TankTypeExtensions.GetGameTankTypes().Select(n => $"`{n.ToString().ToLowerInvariant()}`"))}.");
                            return;
                        }
                    }

                    tanksWithMedal = tanksWithMedal.Where(t => filters.Contains(t.Type)).ToList();
                }

                if (tanksWithMedal.Count <= 0)
                {
                    await ctx.RespondAsync(
                        $"Sorry, {ctx.User.Mention}, the player `{player.Name}` does not have any tank with the `{targetMedal.Name}` medal that also passes the command filters.");
                    return;
                }

                #endregion

                // The top tanks with the medal, finally!
                tanksWithMedal = tanksWithMedal.OrderBy(t => (double) t.Battles / t.Achievements[targetMedal.Code]).Take(25).ToList();

                var sb = new StringBuilder();

                sb.AppendLine($"`{player.Name}`'s best tanks to get the `{targetMedal.Name}` medal, {ctx.User.Mention}:");

                sb.Append("```");

                var maxTankName = tanksWithMedal.Max(t => t.Name.Length);

                sb.AppendLine($"{"Tank".PadRight(maxTankName)} {"Battles".PadLeft(7)} {"Medals".PadLeft(7)}  {"⚔/🎖".PadLeft(7)}");
                foreach(var t in tanksWithMedal)
                {
                    sb.AppendLine($"{t.Name.PadRight(maxTankName)} {t.Battles.ToString("N0").PadLeft(7)} {t.Achievements[targetMedal.Code].ToString("N0").PadLeft(7)}  " +
                                  $"{((double) t.Battles / t.Achievements[targetMedal.Code]).ToString("N0").PadLeft(7)}");
                }

                sb.Append("```");

                var platformPrefix = player.Plataform == Platform.PS ? "ps." : string.Empty;

                var color = player.MonthWn8.ToColor();

                var embed = new DiscordEmbedBuilder
                {
                    Title = $"{player.Name}'s {targetMedal.Name} medals",
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
                        Text = $"Retrieved at {player.Moment:yyyy-MM-dd HH:mm} UTC"
                    }
                };

                await ctx.RespondAsync("", embed: embed);

            }
            catch (Exception ex)
            {
                Log.Error($"{nameof(TankerHistory)}({gamerTag})", ex);
                await ctx.RespondAsync(
                    $"Sorry, {ctx.User.Mention}. There was an error... the *Coder* will be notified of `{ex.Message}`.");
            }
        }


        [Command("TankerHist")]
        [Aliases("PlayerHist", "GamerHist", "PlayerHistory", "GamerHistory", "TankerHistory")]
        [Description("Brief history of a player on the game")]
        public async Task TankerHistory(CommandContext ctx,
            [Description("The *gamer tag* or *PSN Name*")] string gamerTag,
            [Description("The maximum date to consider (`yyyy-MM-dd` format), or `recent`, or `all`")] string maxDate = "all",
            [Description("To display monthly values instead of overall data")] bool monthlyValues = false)
        {
            if (!await CanExecute(ctx, Features.Players))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(gamerTag))
            {
                await ctx.RespondAsync(
                    $"Please specify the *Gamer Tag*, {ctx.User.Mention}. Something like `!w TankerHistory \"{ctx.User.Username.RemoveDiacritics()}\"`, for example.");
                return;
            }

            Log.Debug($"Requesting {nameof(TankerHistory)}({gamerTag}, {maxDate}, {monthlyValues})...");

            try
            {
                var player = await GetPlayer(ctx, gamerTag);
                if (player == null)
                {
                    Log.Debug($"Not found player '{gamerTag}'.");
                    return;
                }

                var provider = new DbProvider(_connectionString);
                var playerHist = provider.GetPlayerHistory(player.Id).OrderByDescending(p => p.Date).ToArray();

                if (playerHist.Length <= 0)
                {
                    await ctx.RespondAsync($"Sorry, {ctx.User.Mention}. This player has no history on this system.");
                    return;
                }

                const int maxHist = 42;

                var l = new List<Player>(maxHist);

                if (maxDate.EqualsCiAi("recent") || maxDate.EqualsCiAi("last"))
                {
                    l.AddRange(playerHist.Take(maxHist));
                }
                else if (maxDate.EqualsCiAi("all"))
                {
                    if (playerHist.Length <= maxHist)
                    {
                        l.AddRange(playerHist);
                    }
                    else
                    {
                        l.Add(playerHist.First());

                        var step = (playerHist.Length - 2) / (maxHist - 2);
                        if (step <= 0)
                        {
                            step = 1;
                        }
                        for (var i = step; i < playerHist.Length && l.Count < (maxHist - 1); i += step)
                        {
                            l.Add(playerHist[i]);
                        }

                        l.Add(playerHist.Last());
                    }
                }
                else if (DateTime.TryParseExact(maxDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    l.AddRange(playerHist.Where(p => p.Date <= date).Take(maxHist));
                }
                else
                {
                    await ctx.RespondAsync($"Sorry, {ctx.User.Mention}. The value `{maxDate}` is not valid. If the gamer tag has spaces, surround it with quotes. Valid values to restrict the history are `all`, `recente`, ou a data on the `yyyy-MM-dd` format.");
                    return;
                }

                var sb = new StringBuilder();

                sb.AppendLine($"History of {Formatter.MaskedUrl(player.Name, new Uri(player.PlayerOverallUrl))}, {ctx.User.Mention}:");
                if (monthlyValues)
                {
                    sb.AppendLine("Displaying monthly values.");
                }
                sb.AppendLine();
                sb.AppendLine("```");
                sb.AppendLine("Date       Clan  Battles  WN8");
                foreach (var p in l)
                {
                    int periodBattles;
                    double wn8;
                    if (monthlyValues)
                    {
                        periodBattles = p.MonthBattles;
                        wn8 = p.MonthWn8;
                    }
                    else
                    {
                        periodBattles = p.TotalBattles;
                        wn8 = p.TotalWn8;
                    }
                    sb.AppendLine($"{p.Date:yyyy-MM-dd} {(p.ClanTag ?? string.Empty).PadLeft(5)} {periodBattles.ToString("N0").PadLeft(7)} {wn8.ToString("N0").PadLeft(6)}");
                }

                sb.AppendLine("```");
                sb.AppendLine();

                var recent = playerHist.First();
                var older = playerHist.Last();

                var days = (recent.Moment - older.Moment).TotalDays;
                var battles = (recent.TotalBattles - older.TotalBattles);

                if (maxDate.EqualsCiAi("all") && !monthlyValues)
                {                                        
                    var deltaPerYear = (recent.TotalWn8 - older.TotalWn8) / days * 365.25;
                    var deltaPer1000Battles = (recent.TotalWn8 - older.TotalWn8) / battles * 1000.0;

                    if (deltaPerYear > 0)
                    {
                        sb.AppendLine($"WN8 improvement per year: {deltaPerYear:N0}");
                    }

                    if (deltaPer1000Battles > 0)
                    {
                        sb.AppendLine($"WN8 improvement per {1000:N0} battles: {deltaPer1000Battles:N0}");
                    }
                }

                var averageOnPeriod = monthlyValues ? playerHist.Average(p => p.MonthWn8) : playerHist.Average(p => p.TotalWn8);
                sb.AppendLine($"WN8 average over {days:N0} days ({battles:N0} battles): {averageOnPeriod:N0}");

                var platformPrefix = player.Plataform == Platform.PS ? "ps." : string.Empty;

                var color = player.MonthWn8.ToColor();

                var embed = new DiscordEmbedBuilder
                {
                    Title = $"{player.Name} history",
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
                        Text = $"Retrieved at {player.Moment:yyyy-MM-dd HH:mm} UTC"
                    }
                };

                await ctx.RespondAsync("", embed: embed);

            }
            catch (Exception ex)
            {
                Log.Error($"{nameof(TankerHistory)}({gamerTag})", ex);
                await ctx.RespondAsync(
                    $"Sorry, {ctx.User.Mention}. There was an error... the *Coder* will be notified of `{ex.Message}`.");
            }
        }


        [Command("TankerTop")]
        [Description("The top tanks of a player")]
        public async Task TankerTop(CommandContext ctx,
            [Description("The *gamer tag* or *PSN Name*")] string gamerTag,
            [Description("Minimum tier")] int minTier = 5,
            [Description("Maximum tier")] int maxTier = 10,
            [Description("Include premiums tanks or not. Use *true*, *false* or *premium*.")]
            string includePremiums = "false")
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

            if (!includePremiums.TryParse(out var premiumSelection))
            {
                await ctx.RespondAsync(
                    $"The *includePremiums* parameter, {ctx.User.Mention}, should be *true*, *false* or *premium*.");
                return;
            }

            try
            {
                var player = await GetPlayer(ctx, gamerTag);
                if (player == null)
                {
                    Log.Debug($"Not found player '{gamerTag}'.");
                    return;
                }

                var top = player.Performance.GetTopTanks(ReferencePeriod.All, 25, minTier, maxTier, premiumSelection)
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
                var platformPrefix = player.Plataform == Platform.PS ? "ps." : string.Empty;

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
                var platformPrefix = player.Plataform == Platform.PS ? "ps." : string.Empty;

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

                Log.Debug($"Requesting {nameof(WhoIAm)}()...");

                var userId = (long)(ctx.User?.Id ?? 0UL);
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
                    $"{ctx.User?.Mention}, as far as I know your {(player.Plataform == Platform.PS ? "PSN Name" : "Gamer Tag")} is `{player.Name}`.");
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

                Log.Debug($"Requesting {nameof(SetWhoIAm)}({gamerTag})...");

                var userId = (long)(ctx.User?.Id ?? 0UL);
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

                Log.Debug($"Requesting {nameof(ForgetWhoIAm)}()...");

                var userId = (long)(ctx.User?.Id ?? 0UL);
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

            Log.Debug($"Requesting {nameof(TankerXP)}({gamerTag}, {minBattles}, {minTier}, {maxTier}, {onlyPremium}, {nation}, {sortByHour})...");

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

                var isPremium = onlyPremium ? true : (bool?)null;
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
                var platformPrefix = player.Plataform == Platform.PS ? "ps." : string.Empty;

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