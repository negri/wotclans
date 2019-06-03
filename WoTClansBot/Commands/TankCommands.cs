using System;
using System.Configuration;
using System.Diagnostics.CodeAnalysis;
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

namespace Negri.Wot.Bot
{
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public class TankCommands : CommandsBase
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(TankCommands));

        private readonly string _connectionString;

        private DateTime _tanksListValidUntilXbox = DateTime.MinValue;
        private DateTime _tanksListValidUntilPs = DateTime.MinValue;

        private readonly object _tankListLock = new object();
        private Tank[] _tanksXbox;
        private Tank[] _tanksPS;

        public TankCommands()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;
        }

        private Tank[] GetTanks(Platform platform)
        {
            lock (_tankListLock)
            {

                if (DateTime.UtcNow > (platform == Platform.PS ? _tanksListValidUntilPs : _tanksListValidUntilXbox))
                {
                    var provider = new DbProvider(_connectionString);
                    if (platform == Platform.PS)
                    {
                        _tanksPS = provider.EnumTanks(Platform.PS).OrderByDescending(t => t.Tier).ThenBy(t => t.TankId).ToArray();
                        _tanksListValidUntilPs = DateTime.UtcNow.AddMinutes(30);
                    }
                    else
                    {
                        _tanksXbox = provider.EnumTanks(Platform.XBOX).OrderByDescending(t => t.Tier).ThenBy(t => t.TankId).ToArray();
                        _tanksListValidUntilXbox = DateTime.UtcNow.AddMinutes(30);
                    }
                }

                return platform == Platform.PS ? _tanksPS : _tanksXbox;
            }
        }

        private Tank FindTank(Platform platform, string tankName, out bool exact)
        {
            var originalName = tankName;
            platform = GetPlatform(tankName, platform, out tankName);

            var all = GetTanks(platform);

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
            Log.Warn($"Not found a tank on {platform} by the string '{originalName}'");

            return null;
        }

        [Command("nations")]
        [Description("The nations that this bot understands as parameters on others commands.")]
        public async Task GetNations(CommandContext ctx)
        {
            if (!await CanExecute(ctx, Features.Tanks))
            {
                return;
            }

            Log.Debug($"Requesting {nameof(GetNations)}()...");

            await ctx.RespondAsync($"Valid nations are: {string.Join(", ", NationExtensions.GetGameNations().Select(n => $"`{n}`"))}");                        
        }

        [Command("tankerTank")]
        [Description("The history of a tanker on a tank")]
        public async Task TankerTank(CommandContext ctx,    
            [Description("The *gamer tag* or *PSN Name*. If it has spaces, enclose it on double quotes.")] string gamerTag,
            [Description("The Tank name, as it appears in battles. If it has spaces, enclose it on double quotes.")][RemainingText] string tankName)
        {
            // Yeah... it's a Player feature but it uses more calculations for tanks...
            if (!await CanExecute(ctx, Features.Players))
            {
                return;
            }

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

            Log.Debug($"Requesting {nameof(TankerTank)}({gamerTag}, {tankName})...");

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

                var tank = FindTank(player.Plataform, tankName, out _);

                if (tank == null)
                {
                    await ctx.RespondAsync($"Can't find a tank with `{tankName}` on the name, {ctx.User.Mention}.");
                    return;
                }

                var tr = provider.GetTanksReferences(tank.Plataform, null, tank.TankId, false, false, false).FirstOrDefault();

                if (tr == null)
                {
                    await ctx.RespondAsync($"Sorry, there is no tank statistics for the `{tank.Name}`, {ctx.User.Mention}.");
                    return;
                }

                var hist = provider.GetPlayerHistoryByTank(player.Plataform, player.Id, tank.TankId).ToArray();
                if (!hist.Any())
                {
                    await ctx.RespondAsync($"Sorry, there is no tank statistics history for the `{tank.Name}` for the player `{gamerTag}`, {ctx.User.Mention}.");
                    return;
                }

                var wn8Expected = provider.GetWn8ExpectedValues(player.Plataform);                

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

                sb.AppendLine($"History of the {Formatter.MaskedUrl(tr.Name, new Uri(tr.Url))}, Tier {tr.Tier.ToRomanNumeral()}, {(tr.IsPremium ? "Premium" : "Regular")}, as played by {Formatter.MaskedUrl(gamerTag, new Uri(player.PlayerOverallUrl))}, {ctx.User.Mention}:");
                sb.AppendLine();
                sb.AppendLine("```");
                sb.AppendLine("Date       Battles Tot.Dmg   WN8");
                foreach (var t in head)
                {
                    sb.AppendLine($"{t.LastBattle:yyyy-MM-dd} {t.Battles.ToString("N0").PadLeft(7)} {t.TotalDamagePerBattle.ToString("N0").PadLeft(7)} {t.Wn8.ToString("N0").PadLeft(6)}");
                }
                if (tail != null)
                {
                    sb.AppendLine("...");
                    foreach (var t in tail)
                    {
                        sb.AppendLine($"{t.LastBattle:yyyy-MM-dd} {t.Battles.ToString("N0").PadLeft(7)} {t.TotalDamagePerBattle.ToString("N0").PadLeft(7)} {t.Wn8.ToString("N0").PadLeft(6)}");
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

                var platformPrefix = tr.Plataform == Platform.PS ? "ps." : string.Empty;
                
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
                        Url = $"https://{platformPrefix}wotclans.com.br"
                    },
                    Footer = new DiscordEmbedBuilder.EmbedFooter
                    {
                        Text = $"Calculated at {player.Moment:yyyy-MM-dd HH:mm} UTC"
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
            [Description("The Tank name, as it appears in battles. If it has spaces, enclose it on double quotes.")][RemainingText] string tankName)
        {
            if (!await CanExecute(ctx, Features.Tanks))
            {
                return;
            }

            Log.Info($"Requesting {nameof(Damage)}({tankName})");

            await ctx.TriggerTypingAsync();

            var cfg = GuildConfiguration.FromGuild(ctx.Guild);

            var tank = FindTank(cfg.Plataform, tankName, out var exact);

            if (tank == null)
            {
                await ctx.RespondAsync($"Can't find a tank with `{tankName}` on the name, {ctx.User.Mention}.");
                return;
            }
            

            var provider = new DbProvider(_connectionString);
            var tr = provider.GetTanksReferences(tank.Plataform, null, tank.TankId, false, false, false).FirstOrDefault();

            if (tr == null)
            {
                await ctx.RespondAsync($"Sorry, there is no tank statistics for the `{tank.Name}`, {ctx.User.Mention}.");
                return;
            }

            var sb = new StringBuilder();

            sb.AppendLine($"Here the **Target Damage** information about the `{tr.Name}`, Tier {tr.Tier.ToRomanNumeral()}, {(tr.IsPremium ? "Premium" : "Regular")}, {ctx.User.Mention}:");

            sb.AppendLine();
            sb.AppendLine($"Average WN8: {(int)Wn8Rating.Average:N0}, Dmg: {tr.TargetDamageAverage:N0}, Piercings: {tr.TargetDamageAveragePiercings}, Shots: {tr.TargetDamageAverageShots};");
            sb.AppendLine($"Good    WN8: {(int)Wn8Rating.Good:N0}, Dmg: {tr.TargetDamageGood:N0}, Piercings: {tr.TargetDamageGoodPiercings}, Shots: {tr.TargetDamageGoodShots};");
            sb.AppendLine($"Great   WN8: {(int)Wn8Rating.Great:N0}, Dmg: {tr.TargetDamageGreat:N0}, Piercings: {tr.TargetDamageGreatPiercings}, Shots: {tr.TargetDamageGreatShots};");
            sb.AppendLine($"Unicum  WN8: {(int)Wn8Rating.Unicum:N0}, Dmg: {tr.TargetDamageUnicum:N0}, Piercings: {tr.TargetDamageUnicumPiercings}, Shots: {tr.TargetDamageUnicumShots};");
            sb.AppendLine($"Super   WN8: {(int)Wn8Rating.SuperUnicum:N0}, Dmg: {tr.TargetDamageSuperUnicum:N0}, Piercings: {tr.TargetDamageSuperUnicumPiercings}, Shots: {tr.TargetDamageSuperUnicumShots}.");
            sb.AppendLine();

            sb.AppendLine($"Recent Averages: WN8: {tr.LastMonth.AverageWn8:N0}, Win Rate: {tr.LastMonth.WinRatio:P1}, Direct Dmg: {tr.LastMonth.DamageDealt:N0}, " +
                $"Total Dmg: {tr.LastMonth.TotalDamage:N0}, Spots: {tr.LastMonth.Spotted:N1}, Kills: {tr.LastMonth.Kills:N1}.");

            var emoji = DiscordEmoji.FromName(ctx.Client, ":exclamation:");

            if (!exact)
            {
                sb.AppendLine();
                sb.AppendLine($"{emoji} If this is *not the tank* you are looking for, try sending the exact short name, the one that appears during battles, or enclosing the name in double quotes.");
            }

            var platformPrefix = tr.Plataform == Platform.PS ? "ps." : string.Empty;

            var color = tr.LastMonth.AverageWn8.ToColor();

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
                    Url = $"https://{platformPrefix}wotclans.com.br"
                },
                Footer = new DiscordEmbedBuilder.EmbedFooter
                {
                    Text = $"Calculated at {tr.Date:yyyy-MM-dd} from {tr.LastMonth.TotalPlayers:N0} recent players and {tr.LastMonth.TotalBattles:N0} battles."
                }
            };

            await ctx.RespondAsync("", embed: embed);
        }


        [Command("moe")]
        [Description("Returns the estimated Total Damage to achieve Marks of Excellence")]
        public async Task Moe(CommandContext ctx,
            [Description("The Tank name, as it appears in battles. If it has spaces, enclose it on double quotes.")][RemainingText] string tankName)
        {
            if (!await CanExecute(ctx, Features.Tanks))
            {
                return;
            }

            Log.Info($"Requesting {nameof(Moe)}({tankName})");

            await ctx.TriggerTypingAsync();

            var cfg = GuildConfiguration.FromGuild(ctx.Guild);

            var tank = FindTank(cfg.Plataform, tankName, out var exact);

            if (tank == null)
            {
                await ctx.RespondAsync($"Can't find a tank with `{tankName}` on the name, {ctx.User.Mention}.");
                return;
            }            

            var provider = new DbProvider(_connectionString);
            var moe = provider.GetMoe(tank.Plataform, null, tank.TankId).FirstOrDefault();

            if (moe == null)
            {
                await ctx.RespondAsync($"Sorry, there is no MoE information for the `{tank.Name}`, {ctx.User.Mention}.");
                return;
            }

            var sb = new StringBuilder();

            sb.AppendLine($"Here the MoE information about the `{moe.Name}`, Tier {moe.Tier.ToRomanNumeral()}, {(moe.IsPremium ? "Premium" : "Regular")}, {ctx.User.Mention}:");
            sb.AppendLine($"```  * 1  mark: {moe.Moe1Dmg:N0} Total Damage");
            sb.AppendLine($" ** 2 marks: {moe.Moe2Dmg:N0} Total Damage");
            sb.AppendLine($"*** 3 marks: {moe.Moe3Dmg:N0} Total Damage```");

            var emoji = DiscordEmoji.FromName(ctx.Client, ":exclamation:");

            if (!exact)
            {
                sb.AppendLine();
                sb.AppendLine($"{emoji} If this is *not the tank* you are looking for, try sending the exact short name, the one that appears during battles, or enclosing the name in double quotes.");
            }

            var platformPrefix = moe.Plataform == Platform.PS ? "ps." : string.Empty;

            var embed = new DiscordEmbedBuilder
            {
                Title = $"{tank.Name} MoE",
                Description = sb.ToString(),
                Color = DiscordColor.Gray,
                ThumbnailUrl = tank.SmallImageUrl,
                Url = $"https://{platformPrefix}wotclans.com.br/Tanks/{tank.TankId}",
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    Name = "WoTClans",
                    Url = $"https://{platformPrefix}wotclans.com.br"
                },
                Footer = new DiscordEmbedBuilder.EmbedFooter
                {
                    Text = $"Calculated at {moe.Date:yyyy-MM-dd} from {moe.NumberOfBattles:N0} battles."
                }
            };

            await ctx.RespondAsync("", embed: embed);
        }

        [Command("leader")]
        [Aliases("leaderboard")]
        [Description("Returns the leaderboard for a tank")]
        public async Task Leader(CommandContext ctx,
            [Description("The Tank name, as it appears in battles. If it has spaces, enclose it on double quotes.")] string tankName,
            [Description("A gamer tag, to be searched on the leaderboard of this tank.  If it has spaces, enclose it on double quotes.")][RemainingText] string gamerTag)
        {
            if (!await CanExecute(ctx, Features.Tanks))
            {
                return;
            }

            Log.Info($"Requesting {nameof(Leader)}({tankName}, {gamerTag})...");

            await ctx.TriggerTypingAsync();

            var cfg = GuildConfiguration.FromGuild(ctx.Guild);

            var tank = FindTank(cfg.Plataform, tankName, out var exact);

            if ((tank == null) && !string.IsNullOrWhiteSpace(gamerTag) && (gamerTag.Length <= 2))
            {
                // Maybe the gamer tag is not a gamer tag, just the final part of a tank name...
                tank = FindTank(cfg.Plataform, tankName + " " + gamerTag, out exact);
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

            int top = 25;
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

            Log.Info($"Requested {nameof(Leader)}({tank.Plataform}.{tank.Name}, {gamerTag})");

            
            var leaderboard = provider.GetLeaderboard(tank.Plataform, tank.TankId, top).ToArray();

            if (!leaderboard.Any())
            {
                await ctx.RespondAsync($"Sorry, there is no leaderboard information for the `{tank.Name}`, {ctx.User.Mention}.");
                return;
            }

            var sb = new StringBuilder();

            sb.AppendLine($"Here the top {Math.Min(leaderboard.Length, 25)} players on the `{tank.Name}`, Tier {tank.Tier.ToRomanNumeral()}, {(tank.IsPremium ? "Premium" : "Regular")}, {ctx.User.Mention}:");

            for (int i = 0; i < Math.Min(leaderboard.Length, 25); i++)
            {
                var l = leaderboard[i];
                sb.AppendLine($"{(i + 1).ToString().PadLeft(3)}: `{l.GamerTag}`, " +
                    $"from `{l.ClanTag}`. Dmg = {l.TotalDamage:N0} = {l.DirectDamage:N0} + {l.DamageAssisted:N0}");
            }

            if (!string.IsNullOrWhiteSpace(gamerTag))
            {
                Leader leader = null;
                int position = 1;
                for (int i = 0; i < leaderboard.Length; i++)
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
                sb.AppendLine($"{emoji} If this is *not the tank* you are looking for, try sending the exact short name, the one that appears during battles, or enclosing the name in double quotes.");
            }

            var platformPrefix = leaderboard.First().Plataform == Platform.PS ? "ps." : string.Empty;

            var embed = new DiscordEmbedBuilder
            {
                Title = $"{tank.Name} Leaderboard",
                Description = sb.ToString(),
                Color = DiscordColor.Gray,
                ThumbnailUrl = tank.SmallImageUrl,
                Url = $"https://{platformPrefix}wotclans.com.br/Tanks/{tank.TankId}",
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    Name = "WoTClans",
                    Url = $"https://{platformPrefix}wotclans.com.br"
                },
                Footer = new DiscordEmbedBuilder.EmbedFooter
                {
                    Text = $"Calculated at {leaderboard.First().Date:yyyy-MM-dd}."
                }
            };

            await ctx.RespondAsync("", embed: embed);
        }

        [Command("leaderByFlag")]
        [Aliases("leaderboardByFlag")]
        [Description("Returns the leaderboard for a tank taking into account the clan flags")]
        public async Task LeaderByFlag(CommandContext ctx,
            [Description("The clan flag **code**. As they appears on https://wotclans.com.br/Flags")] string flagCode,
            [Description("The Tank name, as it appears in battles. If it has spaces, enclose it on double quotes.")] string tankName,
            [Description("A gamer tag, to be searched on the leaderboard of this tank. If it has spaces, enclose it on double quotes.")][RemainingText] string gamerTag)
        {
            if (!await CanExecute(ctx, Features.Tanks))
            {
                return;
            }

            Log.Info($"Requesting {nameof(LeaderByFlag)}({flagCode}, {tankName}, {gamerTag})");

            await ctx.TriggerTypingAsync();

            if (string.IsNullOrWhiteSpace(flagCode) || flagCode.Length != 2)
            {
                await ctx.RespondAsync($"Choose a valid flag code from {Formatter.MaskedUrl("the site", new Uri("https://wotclans.com.br/Flags"), "flags")}, {ctx.User.Mention}.");
                return;
            }
            flagCode = flagCode.RemoveDiacritics().ToLowerInvariant();

            var cfg = GuildConfiguration.FromGuild(ctx.Guild);

            var tank = FindTank(cfg.Plataform, tankName, out var exact);

            if ((tank == null) && !string.IsNullOrWhiteSpace(gamerTag) && (gamerTag.Length <= 2))
            {
                // Maybe the gamer tag is not a gamer tag, just the final part of a tank name...
                tank = FindTank(cfg.Plataform, tankName + " " + gamerTag, out exact);
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

            Log.Info($"Requested {nameof(LeaderByFlag)}({flagCode}, {tank.Plataform}.{tank.Name}, {gamerTag})");

            int top = 25;
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

            
            var leaderboard = provider.GetLeaderboard(tank.Plataform, tank.TankId, top, flagCode).Where(l => (l.ClanFlag ?? string.Empty)
                .ToLowerInvariant() == flagCode).ToArray();

            if (!leaderboard.Any())
            {
                await ctx.RespondAsync($"Sorry, there is no leaderboard information for the `{tank.Name}`, {ctx.User.Mention}.");
                return;
            }

            var sb = new StringBuilder();

            sb.AppendLine($"Here the top {Math.Min(leaderboard.Length, 25)} players from {flagCode.ToUpperInvariant()} on the `{tank.Name}`, Tier {tank.Tier.ToRomanNumeral()}, {(tank.IsPremium ? "Premium" : "Regular")}, {ctx.User.Mention}:");

            for (int i = 0; i < Math.Min(leaderboard.Length, 25); i++)
            {
                var l = leaderboard[i];
                sb.AppendLine($"{(i + 1).ToString().PadLeft(3)}: `{l.GamerTag}`, " +
                    $"from `{l.ClanTag}`. Dmg = {l.TotalDamage:N0} = {l.DirectDamage:N0} + {l.DamageAssisted:N0}");
            }

            if (!string.IsNullOrWhiteSpace(gamerTag))
            {
                Leader leader = null;
                int position = 1;
                for (int i = 0; i < leaderboard.Length; i++)
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
                sb.AppendLine($"{emoji} If this is *not the tank* you are looking for, try sending the exact short name, the one that appears during battles, or enclosing the name in double quotes.");
            }

            var platformPrefix = leaderboard.First().Plataform == Platform.PS ? "ps." : string.Empty;

            var embed = new DiscordEmbedBuilder
            {
                Title = $"{tank.Name} Leaderboard for {flagCode.ToUpperInvariant()}",
                Description = sb.ToString(),
                Color = DiscordColor.Gray,
                ThumbnailUrl = tank.SmallImageUrl,
                Url = $"https://{platformPrefix}wotclans.com.br/Tanks/{tank.TankId}",
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    Name = "WoTClans",
                    Url = $"https://{platformPrefix}wotclans.com.br"
                },
                Footer = new DiscordEmbedBuilder.EmbedFooter
                {
                    Text = $"Calculated at {leaderboard.First().Date:yyyy-MM-dd}."
                }
            };

            await ctx.RespondAsync("", embed: embed);
        }
    }
}