using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;

namespace Negri.Wot.Bot
{
    public class GeneralCommands : CommandsBase
    {
        [Description("Check if I'm alive.")]
        [Command("hi")]
        public async Task Hi(CommandContext ctx)
        {
            if (!await CanExecute(ctx, Features.General))
            {
                return;
            }

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

            var config = GuildConfiguration.FromGuild(ctx.Guild);

            var embed = new DiscordEmbedBuilder
            {
                Title = "WoT Clans",
                Description = $"The best WoT site in the universe is just a click away from you, {ctx.User.Mention}!",
                Color = DiscordColor.DarkGreen,
                Url = $"https://{(config.Plataform == Platform.PS ? "ps." : "")}wotclans.com.br"
            };

            await ctx.RespondAsync("", embed: embed);
        }
    }
}