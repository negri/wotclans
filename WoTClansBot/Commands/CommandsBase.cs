using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;

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
        protected static readonly Regex ClanTagRegex = new Regex("^[A-Z0-9\\-_]{2,5}$", RegexOptions.Compiled);

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