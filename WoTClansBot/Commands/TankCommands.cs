﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Humanizer;
using log4net;
using Negri.Wot.Sql;
using Negri.Wot.Tanks;
using Negri.Wot.WgApi;
using Tank = Negri.Wot.Tanks.Tank;

namespace Negri.Wot.Bot
{
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public class TankCommands : CommandsBase
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(TankCommands));
        private static readonly Random Rand = new Random();

        private readonly string _connectionString;

        private Tank[] _tanks;
        private DateTime _tanksListValidUntil = DateTime.MinValue;
        private readonly object _tankListLock = new object();
        
        public TankCommands()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;
        }

        private Tank[] GetTanks()
        {
            lock (_tankListLock)
            {

                if (DateTime.UtcNow > _tanksListValidUntil)
                {
                    var provider = new DbProvider(_connectionString);
                    {
                        _tanks = provider.EnumTanks().OrderByDescending(t => t.Tier).ThenBy(t => t.TankId).ToArray();
                        _tanksListValidUntil = DateTime.UtcNow.AddMinutes(30);
                    }
                }

                return _tanks;
            }
        }

        public Tank FindTank(string tankName, out bool exact)
        {
            var originalName = tankName;
            
            Nation? nation = null;
            if (tankName.Contains("."))
            {
                // It may have a nation prefix
                foreach (var validNation in NationExtensions.GetGameNations())
                {
                    if (tankName.RemoveDiacritics().ToLowerInvariant().StartsWith(validNation.ToString().ToLowerInvariant() + "."))
                    {
                        nation = validNation;
                        tankName = tankName.Substring(validNation.ToString().Length + 1);
                        break;
                    }
                }
            }

            var all = GetTanks();

            if (nation.HasValue)
            {
                all = all.Where(t => t.Nation == nation.Value).ToArray();
            }

            exact = true;
            var tank = all.FirstOrDefault(t => t.Name.EqualsCiAi(tankName));
            if (tank != null)
            {
                return tank;
            }

            exact = false;

            tankName = tankName.RemoveDiacritics().ToLowerInvariant();
            tank = all.FirstOrDefault(t => t.Name.RemoveDiacritics().ToLowerInvariant().StartsWith(tankName));
            if (tank != null)
            {
                return tank;
            }

            tank = all.FirstOrDefault(t => t.Name.RemoveDiacritics().ToLowerInvariant().Contains(tankName));
            if (tank != null)
            {
                return tank;
            }

            tank = all.FirstOrDefault(t => t.FullName.RemoveDiacritics().ToLowerInvariant().Contains(tankName));
            if (tank != null)
            {
                return tank;
            }

            tankName = Tank.GetFlatString(tankName);

            tank = all.FirstOrDefault(t => t.FlatName == tankName);
            if (tank != null)
            {
                return tank;
            }

            tank = all.FirstOrDefault(t => t.FlatName.Contains(tankName));
            if (tank != null)
            {
                return tank;
            }

            tank = all.FirstOrDefault(t => t.FlatFullName.Contains(tankName));
            if (tank != null)
            {
                return tank;
            }

            // not found
            Log.Warn($"Not found a tank by the string '{originalName}'");

            return null;
        }

        [Command("nations")]
        [Aliases("nation")]
        [Description("The nations that this bot understands as parameters on others commands.")]
        public async Task GetNations(CommandContext ctx)
        {
            if (!await CanExecute(ctx, Features.Tanks))
            {
                return;
            }

            await ctx.RespondAsync("", embed: GetTheEndMessage());

            Log.Debug($"Requesting {nameof(GetNations)}()...");

            await ctx.RespondAsync($"Valid nations are: {string.Join(", ", NationExtensions.GetGameNations().Select(n => $"`{n}`"))}");
        }



        [Command("types")]
        [Aliases("type")]
        [Description("The types of tanks that this bot understands as parameters on others commands.")]
        public async Task GetTypes(CommandContext ctx)
        {
            if (!await CanExecute(ctx, Features.Tanks))
            {
                return;
            }

            await ctx.RespondAsync("", embed: GetTheEndMessage());

            Log.Debug($"Requesting {nameof(GetTypes)}()...");

            await ctx.RespondAsync($"Valid tank types are: {string.Join(", ", TankTypeExtensions.GetGameTankTypes().Select(n => $"`{n}`"))}");
        }

        [Command("tankerTank")]
        [Description("The history of a tanker on a tank")]
        public async Task TankerTank(CommandContext ctx,
            [Description("The *gamer tag* or *PSN Name*. If it has spaces, enclose it on quotes.")] string gamerTag,
            [Description("The Tank name, as it appears in battles. If it has spaces, enclose it on quotes.")]string tankName,
            [Description("Shows additional info explaining the calculated WN8")]bool explainWn8 = false)
        {
            // Yeah... it's a Player feature but it uses more calculations for tanks...
            if (!await CanExecute(ctx, Features.Players))
            {
                return;
            }

            await ctx.RespondAsync("", embed: GetTheEndMessage());

            if (string.IsNullOrWhiteSpace(gamerTag))
            {
                await ctx.RespondAsync($"Please specify the *Gamer Tag*, {ctx.User.Mention}. Something like `!w tankerTank \"{ctx.User.Username.RemoveDiacritics()}\" \"HWK 12\"`, for example.");
                return;
            }

            if (string.IsNullOrWhiteSpace(tankName))
            {
                await ctx.RespondAsync($"Please specify the *Tank Name*, {ctx.User.Mention}. Something like `!w tankerTank \"{ctx.User.Username.RemoveDiacritics()}\" \"HWK 12\"`, for example.");
                return;
            }

            await ctx.TriggerTypingAsync();

            Log.Debug($"Requesting {nameof(TankerTank)}({gamerTag}, {tankName}, {explainWn8})...");

            try
            {
                var provider = new DbProvider(_connectionString);

                var playerCommands = new PlayerCommands();
                var player = await playerCommands.GetPlayer(ctx, gamerTag);
                if (player == null)
                {
                    Log.Debug($"Could not find player {gamerTag}");
                    await ctx.RespondAsync("I could not find a tanker " +
                        $"with the Gamer Tag `{gamerTag}` on the Database, {ctx.User.Mention}. I may not track this player, or the Gamer Tag is wrong.");
                    return;
                }
                gamerTag = player.Name;

                var tank = FindTank(tankName, out _);

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

                var hist = provider.GetPlayerHistoryByTank(player.Platform, player.Id, tank.TankId).ToArray();
                if (!hist.Any())
                {
                    await ctx.RespondAsync($"Sorry, there is no tank statistics history for the `{tank.Name}` for the player `{gamerTag}`, {ctx.User.Mention}.");
                    return;
                }

                var wn8Expected = provider.GetWn8ExpectedValues();

                foreach (var h in hist)
                {
                    h.Wn8 = wn8Expected.CalculateWn8(h);
                }

                TankPlayerStatistics[] head, tail;
                if (hist.Length > 20)
                {
                    head = hist.Take(15).ToArray();
                    tail = hist.Skip(hist.Length - 5).Take(5).ToArray();
                }
                else
                {
                    head = hist;
                    tail = null;
                }

                var sb = new StringBuilder();

                sb.AppendLine($"History of the {Formatter.MaskedUrl(tr.Name, new Uri(tr.Url))}, Tier {tr.Tier.ToRomanNumeral()}, {tr.Nation.GetDescription()}, {(tr.IsPremium ? "Premium" : "Regular")}, as played by {Formatter.MaskedUrl(gamerTag, new Uri(player.PlayerOverallUrl))}, {ctx.User.Mention}:");
                sb.AppendLine();
                sb.AppendLine("```");
                sb.AppendLine("Date       Battles Tot.Dmg   WN8");
                foreach (var t in head)
                {
                    sb.AppendLine($"{t.LastBattle:yyyy-MM-dd} {t.Battles,7:N0} {t.TotalDamagePerBattle,7:N0} {t.Wn8,6:N0}");
                }
                if (tail != null)
                {
                    sb.AppendLine("...");
                    foreach (var t in tail)
                    {
                        sb.AppendLine($"{t.LastBattle:yyyy-MM-dd} {t.Battles,7:N0} {t.TotalDamagePerBattle,7:N0} {t.Wn8,6:N0}");
                    }
                }
                sb.AppendLine("```");
                sb.AppendLine();

                var ptr = player.Performance.All[tr.TankId];

                sb.AppendLine($"**{player.Name}** current values:");
                sb.AppendLine($"Total Damage: {ptr.TotalDamagePerBattle:N0} ");
                sb.AppendLine($"Direct Damage: {ptr.DirectDamagePerBattle:N0} ");
                sb.AppendLine($"Assisted Damage: {ptr.DamageAssistedPerBattle:N0} ");
                sb.AppendLine($"WN8: {ptr.Wn8:N0}; Win Rate: {ptr.WinRate:P1}");
                sb.AppendLine($"Battles: {ptr.Battles:N0}; Hours Battling: {ptr.BattleLifeTime.TotalHours:N0}");
                sb.AppendLine($"Max Kills: {ptr.MaxFrags:N1}; Avg Kills: {ptr.KillsPerBattle:N1}");
                if (tr.LastMonth != null)
                {
                    sb.AppendLine();
                    sb.AppendLine($"**Global Recent** average among {tr.LastMonth.TotalPlayers:N0} players of the {tr.Name}:");
                    sb.AppendLine($"Total Damage: {tr.LastMonth.TotalDamage:N0} ");
                    sb.AppendLine($"Direct Damage: {tr.LastMonth.DamageDealt:N0} ");
                    sb.AppendLine($"Assisted Damage: {tr.LastMonth.DamageAssisted:N0} ");
                    sb.AppendLine($"WN8: {tr.LastMonth.AverageWn8:N0}; Win Rate: {tr.LastMonth.WinRatio:P1}");
                    sb.AppendLine($"Battles: {tr.LastMonth.BattlesPerPlayer:N0}; Hours Battling: {tr.LastMonth.TimePerPlayer.TotalHours:N0}");
                    sb.AppendLine($"Max Kills: {tr.LastMonth.MaxKills:N1}; Avg Kills: {tr.LastMonth.Kills:N1}");
                }

                var color = ptr.Wn8.ToColor();

                var embed = new DiscordEmbedBuilder
                {
                    Title = $"{gamerTag} history with the {tank.Name}",
                    Description = sb.ToString(),
                    Color = new DiscordColor(color.R, color.G, color.B),
                    ThumbnailUrl = tank.SmallImageUrl,
                    Url = player.PlayerOverallUrl,
                    Author = new DiscordEmbedBuilder.EmbedAuthor
                    {
                        Name = "WoTClans",
                        Url = "https://wotclans.com.br"
                    },
                    Footer = new DiscordEmbedBuilder.EmbedFooter
                    {
                        Text = $"Calculated {player.Moment.Humanize()}"
                    }
                };

                await ctx.RespondAsync("", embed: embed);

                if (!explainWn8)
                {
                    return;
                }

                sb.Clear();
                sb.AppendLine($"Overall WN8 Explanation for the {Formatter.MaskedUrl(tr.Name, new Uri(tr.Url))}, Tier {tr.Tier.ToRomanNumeral()}, {tr.Nation.GetDescription()}, {(tr.IsPremium ? "Premium" : "Regular")}, as played by {Formatter.MaskedUrl(gamerTag, new Uri(player.PlayerOverallUrl))}, {ctx.User.Mention}:");
                sb.AppendLine();

                var e = wn8Expected[tank.TankId];
                sb.AppendLine("**Expected Values**");
                sb.AppendLine($"eDmg  = {e.Damage}");
                sb.AppendLine($"eFrag = {e.Frag}");
                sb.AppendLine($"eSpot = {e.Spot}");
                sb.AppendLine($"eWin  = {e.WinRate}");
                sb.AppendLine($"eDef  = {e.Def}");
                sb.AppendLine();

                sb.AppendLine("**Player Values**");
                sb.AppendLine($"pDmg  = {ptr.DirectDamagePerBattle}");
                sb.AppendLine($"pFrag = {ptr.KillsPerBattle}");
                sb.AppendLine($"pSpot = {ptr.SpotsPerBattle}");
                sb.AppendLine($"pWin  = {ptr.WinRate}");
                sb.AppendLine($"pDef  = {ptr.DroppedCapturePointsPerBattle}");
                sb.AppendLine();

                var rDmg = ptr.DirectDamagePerBattle / e.Damage;
                var rFrag = ptr.KillsPerBattle / e.Frag;
                var rSpot = ptr.SpotsPerBattle / e.Spot;
                var rWin = ptr.WinRate / e.WinRate;
                var rDef = ptr.DroppedCapturePointsPerBattle / e.Def;

                sb.AppendLine("**Relative Values**");
                sb.AppendLine($"rDmg  = pDmg/eDmg   = {rDmg}");
                sb.AppendLine($"rFrag = pFrag/eFrag = {rFrag}");
                sb.AppendLine($"rSpot = pSpot/eSpot = {rSpot}");
                sb.AppendLine($"rWin  = pWin/eWin   = {rWin}");
                sb.AppendLine($"rDef  = pDef/eDef   = {rDef}");
                sb.AppendLine();

                var nDmg = Math.Max(0, (rDmg - 0.22) / (1.0 - 0.22));
                var nWin = Math.Max(0, (rWin - 0.71) / (1.0 - 0.71));
                var nFrag = Math.Max(0, Math.Min(nDmg + 0.2, (rFrag - 0.12) / (1 - 0.12)));
                var nSpot = Math.Max(0, Math.Min(nDmg + 0.1, (rSpot - 0.38) / (1 - 0.38)));
                var nDef = Math.Max(0, Math.Min(nDmg + 0.1, (rDef - 0.10) / (1 - 0.10)));

                sb.AppendLine("**Normalized Values**");
                sb.AppendLine($"nDmg  = Max(0, (rDmg - 0.22) / (1.0 - 0.22)) = {nDmg}");
                sb.AppendLine($"nFrag = Max(0, Min(nDmg + 0.2, (rFrag - 0.12) / (1 - 0.12))) = {nFrag}");
                sb.AppendLine($"nSpot = Max(0, Min(nDmg + 0.1, (rSpot - 0.38) / (1 - 0.38))) = {nSpot}");
                sb.AppendLine($"nWin  = Max(0, (rWin - 0.71) / (1.0 - 0.71)) = {nWin}");
                sb.AppendLine($"nDef  = Max(0, Min(nDmg + 0.1, (rDef - 0.10) / (1 - 0.10))) = {nDef}");
                sb.AppendLine();

                var dmgPart = 980.0 * nDmg;
                var winPart = 145.0 * Math.Min(1.8, nWin);
                var fragPart = 210.0 * nDmg * nFrag;
                var spotPart = 155.0 * nFrag * nSpot;
                var defPart = 75.0 * nFrag * nDef;

                sb.AppendLine("**WN8 Parts**");
                sb.AppendLine($"dmgPart = 980 * nDmg = {dmgPart}");
                sb.AppendLine($"fragPart = 210 * nDmg * nFrag = {fragPart}");
                sb.AppendLine($"spotPart = 155 * nFrag * nSpot = {spotPart}");
                sb.AppendLine($"winPart  = 145 * Min(1.8, nWin) = {winPart}");
                sb.AppendLine($"defPart  = 75 * nFrag * nDef = {defPart}");
                sb.AppendLine();

                var wn8 = dmgPart + winPart + fragPart + spotPart + defPart;
                sb.AppendLine($"**WN8** = dmgPart + fragPart + spotPart + winPart + defPart = **{wn8}**");


                embed = new DiscordEmbedBuilder
                {
                    Title = $"{gamerTag} WN8 with the {tank.Name}",
                    Description = sb.ToString(),
                    Color = new DiscordColor(color.R, color.G, color.B),
                    ThumbnailUrl = tank.SmallImageUrl,
                    Url = player.PlayerOverallUrl,
                    Author = new DiscordEmbedBuilder.EmbedAuthor
                    {
                        Name = "WoTClans",
                        Url = "https://wotclans.com.br"
                    },
                    Footer = new DiscordEmbedBuilder.EmbedFooter
                    {
                        Text = $"Calculated {player.Moment.Humanize()}"
                    }
                };

                await ctx.RespondAsync("", embed: embed);

            }
            catch (Exception ex)
            {
                Log.Error($"{nameof(TankerTank)}({gamerTag})", ex);
                await ctx.RespondAsync($"Sorry, {ctx.User.Mention}. There was an error... the *Coder* will be notified of `{ex.Message}`.");
            }
        }


        [Command("damage")]
        [Description("Returns the target damage to achieve a good WN8")]
        public async Task Damage(CommandContext ctx,
            [Description("The Tank name, as it appears in battles. If it has spaces, enclose it on quotes.")][RemainingText] string tankName)
        {
            if (!await CanExecute(ctx, Features.Tanks))
            {
                return;
            }

            await ctx.RespondAsync("", embed: GetTheEndMessage());

            try
            {
                Log.Info($"Requesting {nameof(Damage)}({tankName})");

                await ctx.TriggerTypingAsync();

                var tank = FindTank(tankName, out var exact);

                if (tank == null)
                {
                    await ctx.RespondAsync($"Can't find a tank with `{tankName}` on the name, {ctx.User.Mention}.");
                    return;
                }


                var provider = new DbProvider(_connectionString);
                var tr = provider.GetTanksReferences(null, tank.TankId, includeMoe: false, includeHistogram: false, includeLeaders: false).FirstOrDefault();

                if (tr == null)
                {
                    await ctx.RespondAsync($"Sorry, there is no tank statistics for the `{tank.Name}`, {ctx.User.Mention}.");
                    return;
                }

                var sb = new StringBuilder();

                sb.AppendLine($"Here the **Target Damage** information about the `{tr.Name}`, Tier {tr.Tier.ToRomanNumeral()}, {tr.Nation.GetDescription()}, {(tr.IsPremium ? "Premium" : "Regular")}, {ctx.User.Mention}:");

                sb.AppendLine();
                sb.AppendLine("```");
                sb.AppendLine("Rating   WN8  Damage Piercings Shots");
                sb.AppendLine($"Average {    ((int)Wn8Rating.Average),5:N0} {    tr.TargetDamageAverage,6:N0}        {    tr.TargetDamageAveragePiercings,2:N0}    {tr.TargetDamageAverageShots,2:N0}");
                sb.AppendLine($"Good    {       (int)Wn8Rating.Good,5:N0} {       tr.TargetDamageGood,6:N0}        {       tr.TargetDamageGoodPiercings,2:N0}    {tr.TargetDamageGoodShots,2:N0}");
                sb.AppendLine($"Great   {      (int)Wn8Rating.Great,5:N0} {      tr.TargetDamageGreat,6:N0}        {      tr.TargetDamageGreatPiercings,2:N0}    {tr.TargetDamageGreatShots,2:N0}");
                sb.AppendLine($"Unicum  {     (int)Wn8Rating.Unicum,5:N0} {     tr.TargetDamageUnicum,6:N0}        {     tr.TargetDamageUnicumPiercings,2:N0}    {tr.TargetDamageUnicumShots,2:N0}");
                sb.AppendLine($"Super   {(int)Wn8Rating.SuperUnicum,5:N0} {tr.TargetDamageSuperUnicum,6:N0}        {tr.TargetDamageSuperUnicumPiercings,2:N0}    {tr.TargetDamageSuperUnicumShots,2:N0}");
                sb.AppendLine("```");
                sb.AppendLine();

                if (tr.LastMonth != null)
                {
                    sb.AppendLine($"Recent Averages: WN8: {tr.LastMonth.AverageWn8:N0}, Win Rate: {tr.LastMonth.WinRatio:P1}, Direct Dmg: {tr.LastMonth.DamageDealt:N0}, " +
                                  $"Total Dmg: {tr.LastMonth.TotalDamage:N0}, Spots: {tr.LastMonth.Spotted:N1}, Kills: {tr.LastMonth.Kills:N1}.");
                    sb.AppendLine();
                }
                sb.AppendLine($"Overall Averages: WN8: {tr.AverageWn8:N0}, Win Rate: {tr.WinRatio:P1}, Direct Dmg: {tr.DamageDealt:N0}, " +
                              $"Total Dmg: {tr.TotalDamage:N0}, Spots: {tr.Spotted:N1}, Kills: {tr.Kills:N1}.");

                var emoji = DiscordEmoji.FromName(ctx.Client, ":exclamation:");

                if (!exact)
                {
                    sb.AppendLine();
                    sb.AppendLine($"{emoji} If this is *not the tank* you are looking for, try sending the exact short name, the one that appears during battles, or enclosing the name in quotes.");
                }

                var color = (tr.LastMonth?.AverageWn8 ?? tr.AverageWn8).ToColor();

                var embed = new DiscordEmbedBuilder
                {
                    Title = $"{tank.Name} Target Damage",
                    Description = sb.ToString(),
                    Color = new DiscordColor(color.R, color.G, color.B),
                    ThumbnailUrl = tank.SmallImageUrl,
                    Url = tank.Url,
                    Author = new DiscordEmbedBuilder.EmbedAuthor
                    {
                        Name = "WoTClans",
                        Url = "https://wotclans.com.br"
                    },
                    Footer = new DiscordEmbedBuilder.EmbedFooter
                    {
                        Text = $"Calculated {tr.Date.Humanize()} from {tr.LastMonth?.TotalPlayers ?? tr.TotalPlayers:N0} recent players and {tr.LastMonth?.TotalBattles ?? tr.TotalBattles:N0} battles."
                    }
                };

                await ctx.RespondAsync("", embed: embed);
            }
            catch (Exception ex)
            {
                Log.Error($"Error in {nameof(Damage)}({tankName})", ex);
                await ctx.RespondAsync($"Sorry, {ctx.User.Mention}. There was an error... the *Coder* will be notified of `{ex.Message}`.");
            }
        }


        [Command("moe")]
        [Description("Returns the estimated Total Damage to achieve Marks of Excellence")]
        public async Task Moe(CommandContext ctx,
            [Description("The Tank name, as it appears in battles. If it has spaces, enclose it on quotes.")][RemainingText] string tankName)
        {
            if (!await CanExecute(ctx, Features.Tanks))
            {
                return;
            }

            await ctx.RespondAsync("", embed: GetTheEndMessage());

            Log.Info($"Requesting {nameof(Moe)}({tankName})");

            await ctx.TriggerTypingAsync();

            var tank = FindTank(tankName, out var exact);

            if (tank == null)
            {
                await ctx.RespondAsync($"Can't find a tank with `{tankName}` on the name, {ctx.User.Mention}.");
                return;
            }

            var provider = new DbProvider(_connectionString);
            var moe = provider.GetMoe(null, tank.TankId).FirstOrDefault();

            if (moe == null)
            {
                await ctx.RespondAsync($"Sorry, there is no MoE information for the `{tank.Name}`, {ctx.User.Mention}.");
                return;
            }

            var sb = new StringBuilder();

            sb.AppendLine($"Here the MoE information about the `{moe.Name}`, Tier {moe.Tier.ToRomanNumeral()}, {moe.Nation.GetDescription()}, {(moe.IsPremium ? "Premium" : "Regular")}, {ctx.User.Mention}:");
            sb.AppendLine($"```  * 1  mark: {moe.Moe1Dmg:N0} Total Damage");
            sb.AppendLine($" ** 2 marks: {moe.Moe2Dmg:N0} Total Damage");
            sb.AppendLine($"*** 3 marks: {moe.Moe3Dmg:N0} Total Damage```");

            sb.AppendLine();
            sb.AppendLine($"This data was computed and kindly provided by {Formatter.MaskedUrl("WoTconsole.info", new Uri($"https://www.wotconsole.info/marks/"))}. " +
                          "They use the same algorithm that I used, but with a much larger sample size. And their site is great!");
            

            if (!exact)
            {
                sb.AppendLine();
                var emoji = DiscordEmoji.FromName(ctx.Client, ":exclamation:");
                sb.AppendLine($"{emoji} If this is *not the tank* you are looking for, try sending the exact short name, the one that appears during battles, or enclosing the name in quotes.");
            }

            var embed = new DiscordEmbedBuilder
            {
                Title = $"{tank.Name} MoE",
                Description = sb.ToString(),
                Color = DiscordColor.Gray,
                ThumbnailUrl = tank.SmallImageUrl,
                Url = "https://www.wotconsole.info/marks/",
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    Name = "WoTconsole.info",
                    Url = "https://www.wotconsole.info/marks/"
                },
                Footer = new DiscordEmbedBuilder.EmbedFooter
                {
                    Text = $"Calculated {moe.Date.Humanize()} from {moe.NumberOfBattles:N0} battles."
                }
            };

            await ctx.RespondAsync("", embed: embed);
        }

        [Command("leader")]
        [Aliases("leaderboard")]
        [Description("Returns the leaderboard for a tank")]
        public async Task Leader(CommandContext ctx,
            [Description("The Tank name, as it appears in battles. If it has spaces, enclose it on quotes.")] string tankName,
            [Description("A gamer tag, to be searched on the leaderboard of this tank.  If it has spaces, enclose it on quotes.")][RemainingText] string gamerTag)
        {
            if (!await CanExecute(ctx, Features.Tanks))
            {
                return;
            }

            await ctx.RespondAsync("", embed: GetTheEndMessage());

            Log.Info($"Requesting {nameof(Leader)}({tankName}, {gamerTag})...");

            await ctx.TriggerTypingAsync();

            var tank = FindTank(tankName, out var exact);

            if ((tank == null) && !string.IsNullOrWhiteSpace(gamerTag) && (gamerTag.Length <= 2))
            {
                // Maybe the gamer tag is not a gamer tag, just the final part of a tank name...
                tank = FindTank(tankName + " " + gamerTag, out exact);
                if (tank != null)
                {
                    // yeah... it was not a gamer tag
                    gamerTag = string.Empty;
                }
            }

            if (tank == null)
            {
                await ctx.RespondAsync($"Can't find a tank with `{tankName}` on the name, {ctx.User.Mention}.");
                return;
            }

            var provider = new DbProvider(_connectionString);

            var top = 25;
            gamerTag = gamerTag ?? string.Empty;
            gamerTag = gamerTag.Replace("\"", "").ToLowerInvariant();
            if (!string.IsNullOrEmpty(gamerTag))
            {
                top = 1000;
            }
            if (gamerTag.EqualsCiAi("me"))
            {
                var playerId = provider.GetPlayerIdByDiscordId((long)ctx.User.Id);
                if (playerId.HasValue)
                {
                    var player = provider.GetPlayer(playerId.Value);
                    gamerTag = player.Name;
                }
            }

            Log.Info($"Requested {nameof(Leader)}({tank.Platform}.{tank.Name}, {gamerTag})");


            var leaderboard = provider.GetLeaderboard(tank.TankId, top: top).ToArray();

            if (!leaderboard.Any())
            {
                await ctx.RespondAsync($"Sorry, there is no leaderboard information for the `{tank.Name}`, {ctx.User.Mention}.");
                return;
            }

            var sb = new StringBuilder();

            sb.AppendLine($"Here the top {Math.Min(leaderboard.Length, 25)} players on the `{tank.Name}`, Tier {tank.Tier.ToRomanNumeral()}, {tank.Nation.GetDescription()}, {(tank.IsPremium ? "Premium" : "Regular")}, {ctx.User.Mention}:");
            sb.AppendLine();
            sb.AppendLine("```");

            var size = Math.Min(leaderboard.Length, 25);
            var maxGamerTag = leaderboard.Take(size).Select(l => l.GamerTag.Length).Max();
            if (maxGamerTag < 9)
            {
                maxGamerTag = 9;
            }
            sb.AppendLine($"  # {"Tanker".PadRight(maxGamerTag)} {"Clan",-5} {"Total",6} {"Dir",6} { "Aux",6}");

            for (var i = 0; i < Math.Min(leaderboard.Length, 25); i++)
            {
                var l = leaderboard[i];
                sb.AppendLine($"{(i + 1),3} {l.GamerTag.PadRight(maxGamerTag)} {l.ClanTag,-5} {l.TotalDamage,6:N0} {l.DirectDamage,6:N0} {l.DamageAssisted,6:N0}");
            }

            sb.AppendLine("```");

            if (!string.IsNullOrWhiteSpace(gamerTag))
            {
                Leader leader = null;
                var position = 1;
                for (var i = 0; i < leaderboard.Length; i++)
                {
                    var l = leaderboard[i];
                    if (string.Equals(l.GamerTag, gamerTag, StringComparison.InvariantCultureIgnoreCase))
                    {
                        position = i + 1;
                        leader = l;
                        break;
                    }
                }

                if (leader != null)
                {
                    sb.AppendLine();
                    sb.AppendLine($" {Formatter.MaskedUrl(leader.GamerTag, new Uri(leader.PlayerOverallUrl), leader.GamerTag)} is in {position}th. Dmg = {leader.TotalDamage:N0} = {leader.DirectDamage:N0} + {leader.DamageAssisted:N0}");
                }
                else
                {
                    sb.AppendLine();
                    sb.AppendLine($"`{gamerTag}` does not appear on any place up to {leaderboard.Length:N0}th.");
                }

            }

            var emoji = DiscordEmoji.FromName(ctx.Client, ":exclamation:");

            if (!exact)
            {
                sb.AppendLine();
                sb.AppendLine($"{emoji} If this is *not the tank* you are looking for, try sending the exact short name, the one that appears during battles, or enclosing the name in quotes.");
            }

            var embed = new DiscordEmbedBuilder
            {
                Title = $"{tank.Name} Leaderboard",
                Description = sb.ToString(),
                Color = DiscordColor.Gray,
                ThumbnailUrl = tank.SmallImageUrl,
                Url = $"https://wotclans.com.br/Tanks/{tank.TankId}",
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    Name = "WoTClans",
                    Url = "https://wotclans.com.br"
                },
                Footer = new DiscordEmbedBuilder.EmbedFooter
                {
                    Text = $"Calculated {leaderboard.First().Date.Humanize()}"
                }
            };

            await ctx.RespondAsync("", embed: embed);
        }

        [Command("leaderByFlag")]
        [Aliases("leaderboardByFlag")]
        [Description("Returns the leaderboard for a tank taking into account the clan flags")]
        public async Task LeaderByFlag(CommandContext ctx,
            [Description("The clan flag **code**. As they appears on https://wotclans.com.br/Flags")] string flagCode,
            [Description("The Tank name, as it appears in battles. If it has spaces, enclose it on quotes.")] string tankName,
            [Description("A gamer tag, to be searched on the leaderboard of this tank. If it has spaces, enclose it in quotes.")][RemainingText] string gamerTag)
        {
            if (!await CanExecute(ctx, Features.Tanks))
            {
                return;
            }

            await ctx.RespondAsync("", embed: GetTheEndMessage());

            Log.Info($"Requesting {nameof(LeaderByFlag)}({flagCode}, {tankName}, {gamerTag})");

            await ctx.TriggerTypingAsync();

            if (string.IsNullOrWhiteSpace(flagCode) || flagCode.Length != 2)
            {
                await ctx.RespondAsync($"Choose a valid flag code from {Formatter.MaskedUrl("the site", new Uri("https://wotclans.com.br/Flags"), "flags")}, {ctx.User.Mention}.");
                return;
            }
            flagCode = flagCode.RemoveDiacritics().ToLowerInvariant();

            var tank = FindTank(tankName, out var exact);

            if ((tank == null) && !string.IsNullOrWhiteSpace(gamerTag) && (gamerTag.Length <= 2))
            {
                // Maybe the gamer tag is not a gamer tag, just the final part of a tank name...
                tank = FindTank(tankName + " " + gamerTag, out exact);
                if (tank != null)
                {
                    // yeah... it was not a gamer tag
                    gamerTag = string.Empty;
                }
            }

            if (tank == null)
            {
                await ctx.RespondAsync($"Can't find a tank with `{tankName}` on the name, {ctx.User.Mention}.");
                return;
            }

            Log.Info($"Requested {nameof(LeaderByFlag)}({flagCode}, {tank.Platform}.{tank.Name}, {gamerTag})");

            var top = 25;
            gamerTag = gamerTag ?? string.Empty;
            gamerTag = gamerTag.Replace("\"", "").ToLowerInvariant();
            if (!string.IsNullOrEmpty(gamerTag))
            {
                top = 1000;
            }

            var provider = new DbProvider(_connectionString);

            if (gamerTag.EqualsCiAi("me"))
            {
                var playerId = provider.GetPlayerIdByDiscordId((long)ctx.User.Id);
                if (playerId.HasValue)
                {
                    var player = provider.GetPlayer(playerId.Value);
                    gamerTag = player.Name;
                }
            }


            var leaderboard = provider.GetLeaderboard(tank.TankId, top, flagCode).Where(l => (l.ClanFlag ?? string.Empty)
                                                                                             .ToLowerInvariant() == flagCode).ToArray();

            if (!leaderboard.Any())
            {
                await ctx.RespondAsync($"Sorry, there is no leaderboard information for the `{tank.Name}`, {ctx.User.Mention}.");
                return;
            }

            var sb = new StringBuilder();

            sb.AppendLine($"Here the top {Math.Min(leaderboard.Length, 25)} players from {flagCode.ToUpperInvariant()} on the `{tank.Name}`, Tier {tank.Tier.ToRomanNumeral()}, {tank.Nation.GetDescription()}, {(tank.IsPremium ? "Premium" : "Regular")}, {ctx.User.Mention}:");

            sb.AppendLine();
            sb.AppendLine("```");

            var size = Math.Min(leaderboard.Length, 25);
            var maxGamerTag = leaderboard.Take(size).Select(l => l.GamerTag.Length).Max();
            if (maxGamerTag < 9)
            {
                maxGamerTag = 9;
            }
            sb.AppendLine($"  # {"Tanker".PadRight(maxGamerTag)} {"Clan",-5} {"Total",6} {"Dir",6} { "Aux",6}");

            for (var i = 0; i < Math.Min(leaderboard.Length, 25); i++)
            {
                var l = leaderboard[i];
                sb.AppendLine($"{i + 1,3} {l.GamerTag.PadRight(maxGamerTag)} {l.ClanTag,-5} {l.TotalDamage,6:N0} {l.DirectDamage,6:N0} {l.DamageAssisted,6:N0}");
            }

            sb.AppendLine("```");

            if (!string.IsNullOrWhiteSpace(gamerTag))
            {
                Leader leader = null;
                var position = 1;
                for (var i = 0; i < leaderboard.Length; i++)
                {
                    var l = leaderboard[i];
                    if (l.GamerTag.ToLowerInvariant() == gamerTag.ToLowerInvariant())
                    {
                        position = i + 1;
                        leader = l;
                        break;
                    }
                }

                if (leader != null)
                {
                    sb.AppendLine();
                    sb.AppendLine($" {Formatter.MaskedUrl(leader.GamerTag, new Uri(leader.PlayerOverallUrl), leader.GamerTag)} is in {position}th. Dmg = {leader.TotalDamage:N0} = {leader.DirectDamage:N0} + {leader.DamageAssisted:N0}");
                }
                else
                {
                    sb.AppendLine();
                    sb.AppendLine($"`{gamerTag}` does not appear on any place up to {leaderboard.Length:N0}th.");
                }

            }

            var emoji = DiscordEmoji.FromName(ctx.Client, ":exclamation:");

            if (!exact)
            {
                sb.AppendLine();
                sb.AppendLine($"{emoji} If this is *not the tank* you are looking for, try sending the exact short name, the one that appears during battles, or enclosing the name in quotes.");
            }

            var embed = new DiscordEmbedBuilder
            {
                Title = $"{tank.Name} Leaderboard for {flagCode.ToUpperInvariant()}",
                Description = sb.ToString(),
                Color = DiscordColor.Gray,
                ThumbnailUrl = tank.SmallImageUrl,
                Url = $"https://wotclans.com.br/Tanks/{tank.TankId}",
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    Name = "WoTClans",
                    Url = "https://wotclans.com.br"
                },
                Footer = new DiscordEmbedBuilder.EmbedFooter
                {
                    Text = $"Calculated {leaderboard.First().Date.Humanize()}."
                }
            };

            await ctx.RespondAsync("", embed: embed);
        }


        [Command("randomTank")]
        [Aliases("randTank")]
        [Description("Picks a random tank, so you can chase a objective, or not...")]
        public async Task RandTank(CommandContext ctx,
            [Description("The minimum tier of the tanks")] int minTier = 5,
            [Description("The maximum tier of the tanks")] int maxTier = 10,
            [Description("Nation of the tank, or *any*. Multiple values can be sent using *;* as separators")] string nationFilter = "any",
            [Description("Type of the tank, or *any*. Multiple values can be sent using *;* as separators")] string typeFilter = "any",
            [Description("*Premium*, *regular*, or *any*")] string premiumFilter = "any",
            [Description("The *gamer tag* or *PSN Name*, so it only returns tanks that the player have (or had)")] string gamerTag = null,
            [Description("If *true* then a tank that the given player **hadn't** will be picked")] bool notOnPlayer = false)
        {
            // Yeah... it's a Player feature but it uses more calculations for tanks...
            if (!await CanExecute(ctx, Features.Tanks))
            {
                return;
            }

            await ctx.RespondAsync("", embed: GetTheEndMessage());

            if (minTier < 1)
            {
                await ctx.RespondAsync("The minimum tier is 1.");
                return;
            }

            if (minTier > 10)
            {
                await ctx.RespondAsync("The maximum tier is 1.");
                return;
            }

            if (maxTier < 1)
            {
                await ctx.RespondAsync("The minimum tier is 1.");
                return;
            }

            if (maxTier > 10)
            {
                await ctx.RespondAsync("The maximum tier is 1.");
                return;
            }

            if (maxTier < minTier)
            {
                var temp = maxTier;
                maxTier = minTier;
                minTier = temp;
            }

            await ctx.TriggerTypingAsync();

            Log.Debug($"Requesting {nameof(RandTank)}({minTier}, {maxTier}, {nationFilter}, {typeFilter}, {premiumFilter}, {gamerTag ?? string.Empty}, {notOnPlayer})...");

            try
            {
                var provider = new DbProvider(_connectionString);

                var allTanks = provider.EnumTanks().Where(t => (t.Tier >= minTier) && (t.Tier <= maxTier)).ToList();

                if (!string.IsNullOrWhiteSpace(nationFilter) && !nationFilter.EqualsCiAi("any"))
                {
                    var filtersText = nationFilter.Split(new[] { ',', ';', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var filters = new HashSet<Nation>();
                    foreach (var filterText in filtersText)
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

                    allTanks = allTanks.Where(t => filters.Contains(t.Nation)).ToList();
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

                    allTanks = allTanks.Where(t => filters.Contains(t.Type)).ToList();
                }

                if (!string.IsNullOrWhiteSpace(premiumFilter) && !premiumFilter.EqualsCiAi("any"))
                {
                    if (premiumFilter.EqualsCiAi("regular"))
                    {
                        allTanks = allTanks.Where(t => !t.IsPremium).ToList();
                    }
                    else if (premiumFilter.EqualsCiAi("premium"))
                    {
                        allTanks = allTanks.Where(t => t.IsPremium).ToList();
                    }
                    else
                    {
                        await ctx.RespondAsync(
                            $"Sorry, {ctx.User.Mention}, the premium filter `{premiumFilter}` is not a valid value. Valid values are: `Regular`, `Premium` or `Any`.");
                        return;
                    }
                }

                if (!string.IsNullOrWhiteSpace(gamerTag))
                {
                    var playerCommands = new PlayerCommands();
                    var player = await playerCommands.GetPlayer(ctx, gamerTag);
                    if (player == null)
                    {
                        Log.Debug($"Could not find player {gamerTag}");
                        await ctx.RespondAsync("I could not find a tanker " +
                                               $"with the Gamer Tag `{gamerTag}` on the Database, {ctx.User.Mention}. I may not track this player, or the Gamer Tag is wrong.");
                        return;
                    }
                    gamerTag = player.Name;

                    var playerTanks = player.Performance.All.Select(kv => kv.Key).ToHashSet();

                    allTanks = notOnPlayer
                        ? allTanks.Where(t => !playerTanks.Contains(t.TankId)).ToList()
                        : allTanks.Where(t => playerTanks.Contains(t.TankId)).ToList();

                }

                if (allTanks.Count <= 0)
                {
                    await ctx.RespondAsync(
                        $"Sorry, {ctx.User.Mention}, there are no tanks with the given criteria.");
                    return;
                }

                var number = Rand.Next(0, allTanks.Count);

                var tank = allTanks[number];

                var sb = new StringBuilder();

                sb.AppendLine($"The random tank is the `{tank.Name}`, Tier {tank.Tier.ToRomanNumeral()}, {tank.Nation.GetDescription()}, {(tank.IsPremium ? "Premium" : "Regular")}, {ctx.User.Mention}!");

                var embed = new DiscordEmbedBuilder
                {
                    Title = $"{tank.Name} is the random Tank",
                    Description = sb.ToString(),
                    Color = DiscordColor.Gray,
                    ThumbnailUrl = tank.SmallImageUrl,
                    Url = $"https://wotclans.com.br/Tanks/{tank.TankId}",
                    Author = new DiscordEmbedBuilder.EmbedAuthor
                    {
                        Name = "WoTClans",
                        Url = "https://wotclans.com.br"
                    },
                    Footer = new DiscordEmbedBuilder.EmbedFooter
                    {
                        Text = $"Picked at {DateTime.UtcNow:yyyy-MM-dd HH:mm}."
                    }
                };

                await ctx.RespondAsync("", embed: embed);
            }
            catch (Exception ex)
            {
                Log.Error($"Error on {nameof(RandTank)}({minTier}, {maxTier}, {nationFilter}, {typeFilter}, {premiumFilter}, {gamerTag ?? string.Empty}, {notOnPlayer})", ex);
                await ctx.RespondAsync($"Sorry, {ctx.User.Mention}. There was an error... the *Coder* will be notified of `{ex.Message}`.");
            }
        }
    }
}