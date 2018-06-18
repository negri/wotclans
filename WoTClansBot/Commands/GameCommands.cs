using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using log4net;

namespace Negri.Wot.Bot
{
    public class GameCommands : CommandsBase
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(GameCommands));

        #region Timers

        private readonly object _timerLock = new object();

        private readonly Dictionary<ulong, RunningTimer> _timers = new Dictionary<ulong, RunningTimer>();

        private class RunningTimer
        {
            public ulong UserId { get; set; }

            public DateTime Start { get; set; }

            public int Duration { get; set; }

            public DateTime End => Start.AddSeconds(Duration);
        }

        private bool AddTimer(ulong userId, DateTime start, int duration)
        {
            lock (_timerLock)
            {
                if (_timers.ContainsKey(userId))
                {
                    return false;
                }

                _timers.Add(userId, new RunningTimer() { UserId = userId, Start = start, Duration = duration });
                return true;
            }
        }

        private bool RemoveTimer(ulong userId)
        {
            lock (_timerLock)
            {
                return _timers.Remove(userId);
            }
        }

        private bool HasTimer(ulong userId)
        {
            lock (_timerLock)
            {
                return _timers.ContainsKey(userId);
            }
        }

        private void PruneTimers()
        {
            lock (_timerLock)
            {
                var deads = _timers.Values.Where(t => DateTime.UtcNow > t.End).Select(t => t.UserId).ToList();
                if (!deads.Any())
                {
                    return;
                }

                foreach (var userId in deads)
                {
                    _timers.Remove(userId);
                }
            }
        }

        [Description("Kill a timer that you started.")]
        [Command("kill")]
        public async Task Kill(CommandContext ctx)
        {
            if (!await CanExecute(ctx, Features.Games))
            {
                return;
            }

            await ctx.TriggerTypingAsync();

            var removed = RemoveTimer(ctx.User.Id);
            if (removed)
            {
                await ctx.RespondAsync($"{ctx.User.Mention}, a HEAT shell was sent for your timer...");
            }
            else
            {
                await ctx.RespondAsync($"{ctx.User.Mention}, you don't have any running timers.");
            }
            return;
        }


        [Description("Starts a timer on the range of 30s up to 5min.")]
        [Command("timer")]
        public async Task Timer(CommandContext ctx, [Description("Minutes (up to 5), or Seconds (from 15 to 300")] int minutesOrSeconds)
        {
            if (!await CanExecute(ctx, Features.Games))
            {
                return;
            }

            await ctx.TriggerTypingAsync();

            int seconds;
            if (minutesOrSeconds <= 5)
            {
                seconds = minutesOrSeconds * 60;
            }
            else
            {
                seconds = minutesOrSeconds;
            }

            if (seconds > 5 * 60)
            {
                await ctx.RespondAsync($"{ctx.User.Mention}, the maximum timer interval is 5 minutes.");
                return;
            }
            if (seconds < 15)
            {
                await ctx.RespondAsync($"{ctx.User.Mention}, the minimum timer interval is 30 seconds.");
                return;
            }

            var start = DateTime.UtcNow;
            if (!AddTimer(ctx.User.Id, start, seconds))
            {
                await ctx.RespondAsync($"{ctx.User.Mention}, you already have a timer running. Wait for it finish, or `kill` the timer.");
                return;
            }

            await ctx.RespondAsync($"{ctx.User.Mention}, starting a timer of {seconds}s...");

            int lastAlert = 0;
            var end = DateTime.UtcNow.AddSeconds(seconds);
            while (end > DateTime.UtcNow)
            {
                Task.Delay(TimeSpan.FromMilliseconds(250)).Wait();
                var remaining = end - DateTime.UtcNow;
                int remainingSeconds = (int)remaining.TotalSeconds;
                if ((remainingSeconds % 10 == 0) && (lastAlert != remainingSeconds))
                {
                    if (!HasTimer(ctx.User.Id))
                    {
                        await ctx.RespondAsync($"{ctx.User.Mention}, your timer, with {remainingSeconds}s remaining, was killed... RIP.");
                        return;
                    }

                    if ((remainingSeconds >= 60) && (remainingSeconds % 30 == 0))
                    {
                        await ctx.RespondAsync($"{remainingSeconds}s remaining, {ctx.User.Mention}...");
                        lastAlert = remainingSeconds;
                    }
                    else if ((remainingSeconds >= 5) && (remainingSeconds < 60) && (remainingSeconds % 10 == 0))
                    {
                        await ctx.RespondAsync($"{remainingSeconds}s remaining, {ctx.User.Mention}...");
                        await ctx.TriggerTypingAsync();
                        lastAlert = remainingSeconds;
                    }
                }
            }

            await ctx.RespondAsync($"🏁 Time is up, {ctx.User.Mention}!");

            PruneTimers();
        }

        #endregion

        private static readonly Random Rand = new Random();

        [Command("random")]
        [Description("Returns a random integer number.")]
        public async Task Random(CommandContext ctx, [Description("Minimun, inclusive.")] int min = 1, [Description("Maximum, inclusive.")] int max = 6)
        {
            if (!await CanExecute(ctx, Features.Games))
            {
                return;
            }
           
            var number = Rand.Next(min, max + 1);
            await ctx.RespondAsync($"🎲 {ctx.User.Mention}, your random number between {min} and {max} is: {number}");
        }

        [Command("dice")]
        [Description("Rolls a dice.")]
        public async Task Dice(CommandContext ctx, [Description("Sides on the dice.")] int sides = 6)
        {
            if (!await CanExecute(ctx, Features.Games))
            {
                return;
            }

            var number = Rand.Next(1, sides + 1);
            await ctx.RespondAsync($"🎲 {ctx.User.Mention}, your dice rolled {number}");
        }

        [Command("coin")]
        [Description("Flip a coin.")]
        public async Task Coin(CommandContext ctx)
        {
            if (!await CanExecute(ctx, Features.Games))
            {
                return;
            }

            var cfg = GuildConfiguration.FromGuild(ctx.Guild);

            var isHead = Rand.Next(0, 2) == 0;

            var embed = new DiscordEmbedBuilder
            {
                Title = isHead ? "Head!" : "Tail!",
                Description = $"{ctx.User.Mention}, your coin flipped {(isHead ? "**Head**" : "**Tail**")}!",
                Color = DiscordColor.Goldenrod,
                Url = cfg.Plataform.SiteUrl(),
                ImageUrl = cfg.Plataform.SiteUrl() + $"/images/coin-{(isHead ? "head" : "tail")}.png",
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    Name = "WoTClans",
                    Url = cfg.Plataform.SiteUrl()
                },
                Footer = new DiscordEmbedBuilder.EmbedFooter
                {
                    Text = $"Flipped at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC."
                }
            };

            Log.Debug($"Returned {nameof(Coin)}() = {isHead}");

            await ctx.RespondAsync("", embed: embed);
        }

    }
}