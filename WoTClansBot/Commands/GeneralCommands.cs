using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using log4net;

namespace Negri.Wot.Bot
{
    public class GeneralCommands : CommandsBase
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(GeneralCommands));

        [Description("Check if I'm alive.")]
        [Command("hi")]
        public async Task Hi(CommandContext ctx)
        {
            if (!await CanExecute(ctx, Features.General))
            {
                return;
            }

            Log.Debug($"Requesting {nameof(Hi)}()...");

            await ctx.RespondAsync($"👋 Hello, {ctx.User.Mention}!");
        }

        [Command("site")]
        [Description("Gets the URL to a really good site.")]
        public async Task Site(CommandContext ctx)
        {
            if (!await CanExecute(ctx, Features.General))
            {
                return;
            }

            Log.Debug($"Requesting {nameof(Site)}()...");

            var config = GuildConfiguration.FromGuild(ctx.Guild);

            var embed = new DiscordEmbedBuilder
            {
                Title = "WoT Clans",
                Description = $"The best WoT site in the universe is just a click away from you, {ctx.User.Mention}!",
                Color = DiscordColor.DarkGreen,
                Url = "https://wotclans.com.br"
            };

            await ctx.RespondAsync("", embed: embed);
        }
    }
}