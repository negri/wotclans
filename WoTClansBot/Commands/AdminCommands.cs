using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Negri.Wot.Diagnostics;

namespace Negri.Wot.Bot
{
    [Group("admin")]
    [Description("Administrative commands.")]
    [Hidden]
    [RequireUserPermissions(Permissions.Administrator)]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public class AdminCommands
    {
        [Command("uptime")]
        [Aliases("version", "info")]
        [Description("Shows the amount of time this bot is running.")]
        public async Task Uptime(CommandContext ctx)
        {
            await ctx.TriggerTypingAsync();

            var memory = CpuUsage.GetProcessMemoryInformation();
            var cpu = CpuUsage.GetProcessTotalTime();

            var up = DateTime.UtcNow - cpu.StartTime;
            await ctx.RespondAsync($"I'm up since {cpu.StartTime:yyyy-MM-dd HH:mm:ss} UTC, {ctx.User.Mention}. " +
                                   $"That's {up.TotalDays:N0}.{up.Hours:00}:{up.Minutes:00} ago. " +
                                   $"I was compiled at {RetrieveLinkerTimestamp():yyyy-MM-dd HH:mm}, " +
                                   $"and I'm currently running on a machine called {Environment.MachineName}, " +
                                   $"using {cpu.SinceStartedLoad:P2} of the CPUs and {memory.WorkingSetMB}MB of RAM.");
        }

        [Command("SetDefaultPermission")]
        [Description("Set the default permission to every feature on every channel and role.")]
        public async Task SetDefaultPermission(CommandContext ctx,
            [Description("The default permission")]
            bool permission)
        {
            await ctx.TriggerTypingAsync();

            var configuration = GuildConfiguration.FromGuild(ctx.Guild);
            configuration.PermissionDefault = permission;
            configuration.Save();

            await ctx.RespondAsync(
                $"The default permission to every feature on every channel and role is now `{permission}`.");
        }

        [Command("SetSilentDeny")]
        [Description("Configure that denied commands don't send any feedback to the user.")]
        public async Task SetSilentDeny(CommandContext ctx,
            [Description("If `true` the denies will be silent.")]
            bool useSilentDeny)
        {
            await ctx.TriggerTypingAsync();

            var configuration = GuildConfiguration.FromGuild(ctx.Guild);
            configuration.SilentDeny = useSilentDeny;
            configuration.Save();

            await ctx.RespondAsync($"The Silent Deny is now `{useSilentDeny}`.");
        }

        [Command("SetFeaturePermission")]
        [Description("Set the default permission to a feature.")]
        public async Task SetFeaturePermission(CommandContext ctx,
            [Description("The feature to configure")]
            string feature,
            [Description("The default permission on the feature")]
            bool permission)
        {
            await ctx.TriggerTypingAsync();

            if (!feature.IsFeature())
            {
                var emoji = DiscordEmoji.FromName(ctx.Client, ":no_entry:");
                var embed = new DiscordEmbedBuilder
                {
                    Title = "Invalid Feature",
                    Description =
                        $"{emoji} There is no feature called `{feature}`. Check all the features with `admin features`.",
                    Color = DiscordColor.Red
                };

                await ctx.RespondAsync("", embed: embed);
                return;
            }

            var configuration = GuildConfiguration.FromGuild(ctx.Guild);

            configuration.PermissionByFeature[feature] = permission;

            configuration.Save();

            await ctx.RespondAsync($"The default permission to the `{feature}` feature is now `{permission}`.");
        }

        [Command("SetFeaturePermissionForChannel")]
        [Description("Set the permission to a feature on a specific channel.")]
        public async Task SetFeaturePermissionForChannel(CommandContext ctx,
            [Description("The feature to configure")]
            string feature,
            [Description("The channel to configure (without the #)")]
            string channel,
            [Description("The default permission on the feature")]
            bool permission)
        {
            await ctx.TriggerTypingAsync();

            if (!feature.IsFeature())
            {
                var emoji = DiscordEmoji.FromName(ctx.Client, ":no_entry:");
                var embed = new DiscordEmbedBuilder
                {
                    Title = "Invalid Feature",
                    Description =
                        $"{emoji} There is no feature called `{feature}`. Check all the features with `admin features`.",
                    Color = DiscordColor.Red
                };

                await ctx.RespondAsync("", embed: embed);
                return;
            }

            var configuration = GuildConfiguration.FromGuild(ctx.Guild);

            if (!configuration.PermissionsByFeatureChannel.TryGetValue(feature, out var permissions))
            {
                permissions = new Dictionary<string, bool>();
                configuration.PermissionsByFeatureChannel[feature] = permissions;
            }

            permissions[channel] = permission;

            configuration.Save();

            await ctx.RespondAsync(
                $"The default permission to the `{feature}` feature on the `{channel}` channel is now `{permission}`.");
        }

        [Command("SetFeaturePermissionForRole")]
        [Description("Set the permission to a feature on a specific role.")]
        public async Task SetFeaturePermissionForRole(CommandContext ctx,
            [Description("The feature to configure")]
            string feature,
            [Description("The role to configure")] string role,
            [Description("The default permission on the feature")]
            bool permission)
        {
            await ctx.TriggerTypingAsync();

            if (!feature.IsFeature())
            {
                var emoji = DiscordEmoji.FromName(ctx.Client, ":no_entry:");
                var embed = new DiscordEmbedBuilder
                {
                    Title = "Invalid Feature",
                    Description =
                        $"{emoji} There is no feature called `{feature}`. Check all the features with `admin features`.",
                    Color = DiscordColor.Red
                };

                await ctx.RespondAsync("", embed: embed);
                return;
            }

            var configuration = GuildConfiguration.FromGuild(ctx.Guild);

            if (!configuration.PermissionsByFeatureRole.TryGetValue(feature, out var permissions))
            {
                permissions = new Dictionary<string, bool>();
                configuration.PermissionsByFeatureRole[feature] = permissions;
            }

            permissions[role] = permission;

            configuration.Save();

            await ctx.RespondAsync(
                $"The default permission to the `{feature}` feature on the `{role}` role is now `{permission}`.");
        }

        [Command("SetFeaturePermissionForRoleAndChannel")]
        [Description("Set the permission to a feature on a specific role and channel.")]
        public async Task SetFeaturePermissionForRoleAndChannel(CommandContext ctx,
            [Description("The feature to configure")]
            string feature,
            [Description("The role to configure")] string role,
            [Description("The channel to configure (without the #)")]
            string channel,
            [Description("The default permission on the feature")]
            bool permission)
        {
            await ctx.TriggerTypingAsync();

            if (!feature.IsFeature())
            {
                var emoji = DiscordEmoji.FromName(ctx.Client, ":no_entry:");
                var embed = new DiscordEmbedBuilder
                {
                    Title = "Invalid Feature",
                    Description =
                        $"{emoji} There is no feature called `{feature}`. Check all the features with `admin features`.",
                    Color = DiscordColor.Red
                };

                await ctx.RespondAsync("", embed: embed);
                return;
            }

            var configuration = GuildConfiguration.FromGuild(ctx.Guild);

            if (!configuration.PermissionsByFeatureRoleChannel.TryGetValue(feature, out var permissionsByFeature))
            {
                permissionsByFeature = new Dictionary<string, Dictionary<string, bool>>();
                configuration.PermissionsByFeatureRoleChannel[feature] = permissionsByFeature;
            }

            if (!permissionsByFeature.TryGetValue(role, out var permissionsByRole))
            {
                permissionsByRole = new Dictionary<string, bool>();
                permissionsByFeature[role] = permissionsByRole;
            }

            permissionsByRole[channel] = permission;

            configuration.Save();

            await ctx.RespondAsync(
                $"The permission to the `{feature}` feature on the `{role}` role and channel `#{channel}` is now `{permission}`.");
        }


        [Command("ClearFeaturePermissions")]
        [Description("Clear all the explicit permissions on a feature.")]
        public async Task ClearFeaturePermissions(CommandContext ctx,
            [Description("The feature to clear permissions")]
            string feature)
        {
            await ctx.TriggerTypingAsync();

            if (!feature.IsFeature())
            {
                var emoji = DiscordEmoji.FromName(ctx.Client, ":no_entry:");
                var embed = new DiscordEmbedBuilder
                {
                    Title = "Invalid Feature",
                    Description =
                        $"{emoji} There is no feature called `{feature}`. Check all the features with `admin ListFeatures`.",
                    Color = DiscordColor.Red
                };

                await ctx.RespondAsync("", embed: embed);
                return;
            }

            var configuration = GuildConfiguration.FromGuild(ctx.Guild);

            configuration.PermissionByFeature.Remove(feature);
            configuration.PermissionsByFeatureChannel.Remove(feature);
            configuration.PermissionsByFeatureRole.Remove(feature);
            configuration.PermissionsByFeatureRoleChannel.Remove(feature);

            configuration.Save();

            await ctx.RespondAsync($"All the explicit permissions to the `{feature}` feature were cleared.");
        }

        [Command("ResetPermissions")]
        [Description("Reset all permissions on the server.")]
        public async Task ResetPermissions(CommandContext ctx)
        {
            await ctx.TriggerTypingAsync();

            var cfg = GuildConfiguration.FromGuild(ctx.Guild);

            cfg.PermissionByFeature.Clear();
            cfg.PermissionsByFeatureChannel.Clear();
            cfg.PermissionsByFeatureRole.Clear();
            cfg.PermissionsByFeatureRoleChannel.Clear();
            cfg.PermissionDefault = true;

            cfg.Save();

            await ctx.RespondAsync("All permissions were reset. Any command can be called by anyone everywhere.");
        }

        [Command("ListFeatures")]
        [Description("List all features on the bot.")]
        public async Task ListFeatures(CommandContext ctx)
        {
            await ctx.RespondAsync($"The features are: {string.Join(", ", Features.GetAll().Select(s => $"`{s}`"))}.");
        }

        [Command("ListPermissions")]
        [Description("List all explicit configured permissions.")]
        public async Task ListPermissions(CommandContext ctx)
        {
            await ctx.TriggerTypingAsync();

            var cfg = GuildConfiguration.FromGuild(ctx.Guild);

            var sb = new StringBuilder();

            sb.AppendLine("These are the permissions on this server, on the order they are evaluated:");
            sb.AppendLine();

            sb.AppendLine("1) Explicit permissions on a *feature* for a given *role* **and** *channel*:");
            if (!cfg.PermissionsByFeatureRoleChannel.Any())
            {
                sb.AppendLine("  No explicit permissions.");
            }
            else
            {
                foreach (var byFeature in cfg.PermissionsByFeatureRoleChannel.OrderBy(kv => kv.Key))
                {
                    foreach (var byRole in byFeature.Value.OrderBy(kv => kv.Key))
                    {
                        foreach (var byChannel in byRole.Value.OrderBy(kv => kv.Key))
                        {
                            sb.AppendLine(
                                $"  Feature `{byFeature.Key}`, role `{byRole.Key}`, channel `#{byChannel.Key}`: {(byChannel.Value ? "allow" : "deny")}");
                        }
                    }
                }
            }

            sb.AppendLine();

            sb.AppendLine("2) Explicit permissions on a *feature* for a given *role*:");
            if (!cfg.PermissionsByFeatureRole.Any())
            {
                sb.AppendLine("  No explicit permissions.");
            }
            else
            {
                foreach (var byFeature in cfg.PermissionsByFeatureRole.OrderBy(kv => kv.Key))
                {
                    foreach (var byRole in byFeature.Value.OrderBy(kv => kv.Key))
                    {
                        sb.AppendLine(
                            $"  Feature `{byFeature.Key}`, role `{byRole.Key}`: {(byRole.Value ? "allow" : "deny")}");
                    }
                }
            }

            sb.AppendLine();

            sb.AppendLine("3) Explicit permissions on a *feature* for a given *channel*:");
            if (!cfg.PermissionsByFeatureChannel.Any())
            {
                sb.AppendLine("  No explicit permissions.");
            }
            else
            {
                foreach (var byFeature in cfg.PermissionsByFeatureChannel.OrderBy(kv => kv.Key))
                {
                    foreach (var byChannel in byFeature.Value.OrderBy(kv => kv.Key))
                    {
                        sb.AppendLine(
                            $"  Feature `{byFeature.Key}`, channel `#{byChannel.Key}`: {(byChannel.Value ? "allow" : "deny")}");
                    }
                }
            }

            sb.AppendLine();

            sb.AppendLine("4) Explicit permissions on a *feature*:");
            if (!cfg.PermissionByFeature.Any())
            {
                sb.AppendLine("  No explicit permissions.");
            }
            else
            {
                foreach (var byFeature in cfg.PermissionByFeature.OrderBy(kv => kv.Key))
                {
                    sb.AppendLine($"  Feature `{byFeature.Key}`: {(byFeature.Value ? "allow" : "deny")}");
                }
            }

            sb.AppendLine();

            sb.AppendLine($"5) The *Global Permission*: {(cfg.PermissionDefault ? "allow" : "deny")}");
            sb.AppendLine();

            await ctx.RespondAsync(sb.ToString());
        }


        [Command("TestPermissions")]
        [Description("Test the permission on a feture to a given channel and role.")]
        public async Task TestPermissions(CommandContext ctx,
            [Description("The feature")] string feature,
            [Description("The role")] string role,
            [Description("The channel")] string channel)
        {
            await ctx.TriggerTypingAsync();

            if (!feature.IsFeature())
            {
                var emoji = DiscordEmoji.FromName(ctx.Client, ":no_entry:");
                var embed = new DiscordEmbedBuilder
                {
                    Title = "Invalid Feature",
                    Description =
                        $"{emoji} There is no feature called `{feature}`. Check all the features with `admin ListFeatures`.",
                    Color = DiscordColor.Red
                };

                await ctx.RespondAsync("", embed: embed);
                return;
            }

            var configuration = GuildConfiguration.FromGuild(ctx.Guild);

            var result = configuration.CanCallerExecute(feature, new[] {role}, channel, out var reason);
            await ctx.RespondAsync(
                $"The feature `{feature}` is **{(result ? "allowed" : "denied")}** on role `{role}` in the channel `#{channel}`. Explanation: {reason}");
        }

        [Command("SetPlataform")]
        [Description("Set the plataform this Guild is interested.")]
        public async Task SetPlataform(CommandContext ctx, [Description("Plataform")] string plataform)
        {
            if (!Enum.TryParse(plataform, true, out Platform plat))
            {
                var emoji = DiscordEmoji.FromName(ctx.Client, ":no_entry:");
                var embed = new DiscordEmbedBuilder
                {
                    Title = "Wrong parameter",
                    Description = $"{emoji} The plataform should be `{Platform.XBOX}` or `{Platform.PS}`.",
                    Color = DiscordColor.Red
                };
                await ctx.RespondAsync("", embed: embed);
                return;
            }

            await ctx.TriggerTypingAsync();

            var configuration = GuildConfiguration.FromGuild(ctx.Guild);

            configuration.Plataform = plat;
            configuration.Name = ctx.Guild.Name;
            configuration.Region = ctx.Guild.RegionId;

            configuration.Save();
            await ctx.RespondAsync($"The plataform is now {plataform}.");
        }

        /// <summary>
        ///     Retrieves the linker timestamp.
        /// </summary>
        /// <remarks>
        ///     http://stackoverflow.com/questions/1600962/displaying-the-build-date
        /// </remarks>
        private static DateTime RetrieveLinkerTimestamp()
        {
            var filePath = Assembly.GetCallingAssembly().Location;
            const int peHeaderOffset = 60;
            const int linkerTimestampOffset = 8;
            var b = new byte[2048];
            Stream s = null;

            try
            {
                s = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                s.Read(b, 0, 2048);
            }
            finally
            {
                s?.Close();
            }

            var i = BitConverter.ToInt32(b, peHeaderOffset);
            var secondsSince1970 = BitConverter.ToInt32(b, i + linkerTimestampOffset);
            var dt = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            dt = dt.AddSeconds(secondsSince1970);
            dt = dt.ToLocalTime();
            return dt;
        }
    }
}