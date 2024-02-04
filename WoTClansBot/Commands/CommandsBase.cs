using System.Drawing;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using Negri.Wot.WgApi;

namespace Negri.Wot.Bot
{
    /// <summary>
    /// Base class for Commands
    /// </summary>
    public abstract class CommandsBase
    {



        /// <summary>
        ///     Regex for clan tags
        /// </summary>
        protected static readonly Regex ClanTagRegex = new Regex("^[A-Z0-9\\-_]{2,5}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);


        protected DiscordEmbedBuilder GetTheEndMessage()
        {
            const string msg = "Sorry, The WoTClans Site and Discord bot **are gone.**\r\n\r\n" +
                "The command may, or may not, work for a few more days.\r\n\r\n" +
                "Thanks for all this time!\r\n\r\n" +
                "[]'s *JP Negri Coder*";

            var embed = new DiscordEmbedBuilder
            {
                Title = "This bot is going away...",
                Description = msg,
                Color = DiscordColor.Yellow,
                Url = "https://github.com/negri/wotclans/blob/master/the-end.en.md",
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    Name = "JP Negri Coder"
                },
                Footer = new DiscordEmbedBuilder.EmbedFooter
                {
                    Text = $"Sent at {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC"
                }
            };

            return embed;
        }

        protected async Task<bool> CanExecute(CommandContext ctx, string feature)
        {
            var cfg = GuildConfiguration.FromGuild(ctx.Guild);
            if (!cfg.CanCallerExecute(feature, ctx.Member, ctx.Channel, out var reason))
            {
                if (!cfg.SilentDeny)
                {
                    var emoji = DiscordEmoji.FromName(ctx.Client, ":no_entry:");
                    var embed = new DiscordEmbedBuilder
                    {
                        Title = "Access denied",
                        Description = $"{emoji} {reason}",
                        Color = DiscordColor.Red
                    };

                    await ctx.RespondAsync("", embed: embed);
                }
                
                return await Task.Run(() => false);
            }

            return await Task.Run(() => true);
        }

        protected Platform GetPlatform(string s, Platform defaultPlatform, out string clean)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                clean = string.Empty;
                return defaultPlatform;
            }
            
            s = s.Trim('\"', ' ', '\t', '\r', '\n', '\'', '`', '´');
            s = s.RemoveDiacritics().ToLowerInvariant();

            if (s.StartsWith("ps."))
            {
                clean = s.Substring(3);
                return Platform.PS;                
            }
            else if (s.StartsWith("ps4."))
            {
                clean = s.Substring(4);
                return Platform.PS;                
            }
            else if (s.StartsWith("xbox."))
            {
                clean = s.Substring(5);
                return Platform.XBOX;                
            }
            else if (s.StartsWith("x."))
            {
                clean = s.Substring(2);
                return Platform.XBOX;                
            }
            else if (s.StartsWith("p."))
            {
                clean = s.Substring(2);
                return Platform.PS;                
            }

            clean = s;
            return defaultPlatform;
        }
    }
}