using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using DSharpPlus.Entities;
using log4net;
using Newtonsoft.Json;

namespace Negri.Wot.Bot
{
    
    /// <summary>
    /// Guild configurations
    /// </summary>
    public class GuildConfiguration
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(GuildConfiguration));

        public GuildConfiguration() { }

        public GuildConfiguration(ulong id)
        {
            Id = id;
        }

        /// <summary>
        /// Date that the bot joined the Guild
        /// </summary>
        public DateTime JoinDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Dircord Guild id
        /// </summary>
        public ulong Id { get; set; }

        /// <summary>
        /// Guild preferred plataform
        /// </summary>
        public Plataform Plataform { get; set; } = Plataform.XBOX;

        /// <summary>
        /// Guild Name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Guild Region
        /// </summary>
        public string Region { get; set; }

        /// <summary>
        /// Members count
        /// </summary>
        public int MemberCount { get; set; }

        #region Permissions

        /// <summary>
        /// If denies on features should be silent
        /// </summary>
        public bool SilentDeny { get; set; } = false;

        /// <summary>
        /// If any feature and role is allowed by default
        /// </summary>
        public bool PermissionDefault { get; set; } = true;

        /// <summary>
        /// Permission by feature
        /// </summary>
        public Dictionary<string, bool> PermissionByFeature { get; set; } = new Dictionary<string, bool>();

        /// <summary>
        /// Permission by feature and channel
        /// </summary>
        public Dictionary<string, Dictionary<string, bool>> PermissionsByFeatureChannel = new Dictionary<string, Dictionary<string, bool>>();

        /// <summary>
        /// Permission by feature and role
        /// </summary>
        public Dictionary<string, Dictionary<string, bool>> PermissionsByFeatureRole = new Dictionary<string, Dictionary<string, bool>>();

        /// <summary>
        /// Permission by feature and role and channel
        /// </summary>
        public Dictionary<string, Dictionary<string, Dictionary<string, bool>>> PermissionsByFeatureRoleChannel = new Dictionary<string, Dictionary<string, Dictionary<string, bool>>>();

        public bool CanCallerExecute(string feature, IEnumerable<string> roles, string channel, out string reason)
        {
            if (string.IsNullOrWhiteSpace(feature))
            {
                reason = "Invoked with a null feature";
                return true;
            }

            if (!Features.IsFeature(feature))
            {
                reason = "Invoked with a invalid feature.";
                return true;
            }

            if (string.IsNullOrWhiteSpace(channel))
            {
                reason = "Channel name is empty.";
                return true;
            }

            bool permission;

            // Check Feature and Roles and Channels
            if (PermissionsByFeatureRoleChannel.TryGetValue(feature, out var detailedPermissionsOnFeature))
            {
                foreach (var role in roles.OrderBy(s => s))
                {
                    if (detailedPermissionsOnFeature.TryGetValue(role, out var permissionsOnRole))
                    {
                        if (permissionsOnRole.TryGetValue(channel, out permission))
                        {
                            reason = $"Explicit {(permission ? "allow" : "deny")} on role `{role}` and channel `#{channel}`.";
                            return permission;
                        }
                    }
                }
            }

            // Check Feature and Roles
            if (PermissionsByFeatureRole.TryGetValue(feature, out var permissionsOnFeature))
            {
                foreach (var role in roles.OrderBy(s => s))
                {
                    if (permissionsOnFeature.TryGetValue(role, out permission))
                    {
                        reason = $"Explicit {(permission ? "allow" : "deny")} on role `{role}`.";
                        return permission;
                    }
                }
            }

            // Check Feature and Channel
            if (PermissionsByFeatureChannel.TryGetValue(feature, out permissionsOnFeature))
            {
                if (permissionsOnFeature.TryGetValue(channel, out permission))
                {
                    reason = $"Explicit {(permission ? "allow" : "deny")} on channel `#{channel}`.";
                    return permission;
                }
            }

            // Check Default permission by role
            if (PermissionByFeature.TryGetValue(feature, out permission))
            {
                reason = $"Explicity {(permission ? "allow" : "deny")} on feature `{feature}`.";
                return permission;
            }

            // The global default
            reason = $"Default is {(permission ? "allow" : "deny")}.";
            return PermissionDefault;
        }

        public bool CanCallerExecute(string feature, DiscordMember member, DiscordChannel channel, out string reason)
        {
            if (member == null)
            {
                reason = "Member is null.";
                return true;
            }
            var roles = new HashSet<string>((member.Roles ?? new List<DiscordRole>()).Select(dr => dr.Name));

            if (channel == null)
            {
                reason = "Channel is null.";
                return true;
            }

            return CanCallerExecute(feature, roles, channel.Name, out reason);
        }

        #endregion

        public void Save()
        {
            var directory = ConfigurationManager.AppSettings["ConfigDir"];
            var configFile = Path.Combine(directory, $"{Id}.json");
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(configFile, json, Encoding.UTF8);
        }

        public static GuildConfiguration FromGuild(DiscordGuild guild)
        {
            if (guild == null)
            {
                // DM, returns a special permissible configuration
                return new GuildConfiguration() { PermissionDefault = true };
            }

            var directory = ConfigurationManager.AppSettings["ConfigDir"];
            var configFile = Path.Combine(directory, $"{guild.Id}.json");

            if (!File.Exists(configFile))
            {
                return new GuildConfiguration(guild.Id)
                {
                    Name = guild.Name,
                    Region = guild.RegionId
                };
            }

            var json = File.ReadAllText(configFile, Encoding.UTF8);
            var config = JsonConvert.DeserializeObject<GuildConfiguration>(json);

            // update some info
            config.Id = guild.Id;
            config.MemberCount = guild.MemberCount;
            config.Name = guild.Name;
            config.Region = guild.RegionId;

            return config;
        }

    }
}