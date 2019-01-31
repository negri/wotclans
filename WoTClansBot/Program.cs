using System;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using log4net;

namespace Negri.Wot.Bot
{
    internal class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Program));

        private static DiscordClient _discord;
        private static CommandsNextModule _commands;

        private static int Main()
        {
            try
            {
                Log.Info("Starting...");
                var result = MainAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                Log.Info("End.");
                return result;
            }
            catch (Exception ex)
            {
                Log.Fatal("Main", ex);
                return 1;
            }
        }

        private static async Task<int> MainAsync()
        {
            var token = ConfigurationManager.AppSettings["Token"];

            _discord = new DiscordClient(new DiscordConfiguration
            {
                Token = token,
                TokenType = TokenType.Bot,
                LogLevel = LogLevel.Debug,
                UseInternalLogHandler = false
            });

            _discord.Ready += DiscordOnReady;
            _discord.GuildAvailable += DiscordOnGuildAvailable;
            _discord.ClientErrored += DiscordOnClientErrored;
            _discord.DebugLogger.LogMessageReceived += DebugLoggerOnMessageReceived;
            _discord.Heartbeated += DiscordHeartbeated;

            var prefix = ConfigurationManager.AppSettings["Prefix"];

            var ccfg = new CommandsNextConfiguration
            {
                StringPrefix = prefix,
                CaseSensitive = false
            };
            
            _commands = _discord.UseCommandsNext(ccfg);

            _commands.CommandExecuted += CommandsOnCommandExecuted;
            _commands.CommandErrored += CommandsOnCommandErrored;

            _commands.RegisterCommands<GeneralCommands>();
            _commands.RegisterCommands<GameCommands>();            
            _commands.RegisterCommands<ClanCommands>();
            _commands.RegisterCommands<TankCommands>();
            _commands.RegisterCommands<PlayerCommands>();
            _commands.RegisterCommands<CoderCommands>();
            _commands.RegisterCommands<AdminCommands>();

            await _discord.ConnectAsync();

            Log.Info("Connected. Waiting...");

            while (true)
            {
                await Task.Delay(TimeSpan.FromMinutes(1.0));
                var heartBeatAge = DateTime.UtcNow - _lastHeartBeat;
                if (heartBeatAge.TotalMinutes > 5)
                {
                    break;
                }
            }

            Log.Fatal("5 minutes without a heart beat. He is dead, Jin!");
            return 1;
        }

        private static DateTime _lastHeartBeat = DateTime.UtcNow;

        private static Task DiscordHeartbeated(HeartbeatEventArgs e)
        {            
            _lastHeartBeat = DateTime.UtcNow;
            return Task.CompletedTask;
        }

        private static void DebugLoggerOnMessageReceived(object sender, DebugLogMessageEventArgs e)
        {
            switch (e.Level)
            {
                case LogLevel.Debug:
                    if (e.Application != "Websocket")
                    {
                        Log.Debug($"{e.Application}: {e.Message}");
                    }                    
                    break;
                case LogLevel.Info:
                    Log.Info($"{e.Application}: {e.Message}");
                    break;
                case LogLevel.Warning:
                    Log.Warn($"{e.Application}: {e.Message}");
                    break;
                case LogLevel.Error:
                    Log.Error($"{e.Application}: {e.Message}");
                    break;
                case LogLevel.Critical:
                    Log.Fatal($"{e.Application}: {e.Message}");
                    break;
                default:
                    Log.Error($"{e.Application}: {e.Message}");
                    break;
            }
        }

        private static async Task CommandsOnCommandErrored(CommandErrorEventArgs args)
        {
            if (args == null)
            {
                Log.Error($"Mystery call to {nameof(CommandsOnCommandErrored)} without any details.");
                return;
            }

            if (args.Exception == null)
            {
                Log.Error($"Mystery call to {nameof(CommandsOnCommandErrored)} without an exception.");
                return;
            }

            try
            {
                switch (args.Exception)
                {
                    case ChecksFailedException checksFailedException:
                    {
                        Log.Warn(
                            $"{nameof(ChecksFailedException)}: {checksFailedException.Command.QualifiedName}: {string.Join(",", checksFailedException.FailedChecks.Select(c => c.ToString()))}",
                            checksFailedException);

                        var emoji = DiscordEmoji.FromName(checksFailedException.Context.Client, ":no_entry:");
                        var embed = new DiscordEmbedBuilder
                        {
                            Title = "Access denied",
                            Description = $"{emoji} You do not have the permissions required to execute this command.",
                            Color = DiscordColor.Red
                        };

                        if (args.Context != null)
                        {
                            await args.Context.RespondAsync("", embed: embed);
                        }
                        return;
                    }
                    case CommandNotFoundException commandNotFoundException:
                        Log.Error($"{nameof(CommandNotFoundException)} : {commandNotFoundException.Command}: { commandNotFoundException.Message}", commandNotFoundException);
                        await args.Context.RespondAsync("Sorry, I don't recognize the command that you issued. Take a look on `!w help` if needed.");
                        return;
                    case ArgumentException exa:
                    {
                        if (args.Context != null)
                        {
                            await args.Context.RespondAsync($"{exa.Message}");
                        }
                        return;
                    }
                    default:
                        Log.Error($"[{args?.Context?.User?.Username ?? "<???>"}] tried executing {args.Command?.QualifiedName ?? "<???>"} on [{args.Context?.Guild?.Name ?? "DM"}].{args.Context?.Guild?.Id ?? 0} #{args.Context?.Channel?.Name ?? "DM"}.", args.Exception);
                        break;
                }
            }
            catch (Exception exx)
            {
                Log.Error("Error reporting a error!", exx);
                Log.Error(args.Command?.QualifiedName ?? "<???>", args.Exception);
            }
        }

        private static Task CommandsOnCommandExecuted(CommandExecutionEventArgs args)
        {
            Log.Debug($"[{args.Context.User.Username}] successfully executed {args.Command.QualifiedName} on [{args.Context.Guild?.Name ?? "DM"}].{args.Context.Guild?.Id ?? 0} #{args.Context.Channel?.Name ?? "DM"}.");
            return Task.CompletedTask;
        }

        private static Task DiscordOnClientErrored(ClientErrorEventArgs args)
        {
            Log.Error("ClientErrored", args.Exception);
            return Task.CompletedTask;
        }

        private static Task DiscordOnGuildAvailable(GuildCreateEventArgs args)
        {
            Log.Info($"Guild Available: {args.Guild.Name}.{args.Guild.RegionId}.{args.Guild.Id} with {args.Guild.MemberCount} members.");

            var config = GuildConfiguration.FromGuild(args.Guild);
            config.Save();

            return Task.CompletedTask;
        }

        private static Task DiscordOnReady(ReadyEventArgs e)
        {
            Log.Info("Client Ready");
            return Task.CompletedTask;
        }
    }
}
