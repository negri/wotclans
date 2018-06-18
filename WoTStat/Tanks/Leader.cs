using System;
using System.Globalization;
using Negri.Wot.WgApi;
using Newtonsoft.Json;

namespace Negri.Wot.Tanks
{
    /// <summary>
    /// A tank Leader
    /// </summary>
    public class Leader : Tank
    {
        public DateTime Date { get; set; }

        /// <summary>
        /// Ordem da Liderança
        /// </summary>
        public int Order { get; set; }
        
        public long PlayerId { get; set; }

        public string GamerTag { get; set; }

        public DateTime LastBattle { get; set; }

        public TimeSpan BattleTime { get; set; }

        public long Battles { get; set; }

        public long ClanId { get; set; }

        public string ClanTag { get; set; }

        public double TotalDamage { get; set; }

        public string ClanFlag { get; set; }

        public double DamageAssisted { get; set; }

        [JsonIgnore]
        public double DirectDamage => TotalDamage - DamageAssisted;

        public double Kills { get; set; }

        public long MaxKills { get; set; }

        public double Spotted { get; set; }

        /// <summary>
        /// The player overall Url
        /// </summary>
        [JsonIgnore]
        public string PlayerOverallUrl => $"{ClanUrl}/Commanders/{PlayerId}/All";

        /// <summary>
        /// The clan url
        /// </summary>
        [JsonIgnore]
        public string ClanUrl => $"https://{(Plataform == Plataform.PS ? "ps." : "")}wotclans.com.br/Clan/{ClanTag}";

        public override string ToString()
        {
            return $"{Order};{GamerTag};{ClanTag};{Name};{Tier};{TotalDamage.ToString("R", CultureInfo.InvariantCulture)}";
        }

        /// <summary>
        /// <c>true</c> se <paramref name="s"/> faz parte do nome do tanque, do clã ou do jogador
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public bool IsGlobalMatch(string s)
        {
            s = s.RemoveDiacritics().ToLowerInvariant();
            if (Name.RemoveDiacritics().ToLowerInvariant().Contains(s))
            {
                return true;
            }
            if (FullName.RemoveDiacritics().ToLowerInvariant().Contains(s))
            {
                return true;
            }
            if (ClanTag.RemoveDiacritics().ToLowerInvariant().Contains(s))
            {
                return true;
            }
            if (GamerTag.RemoveDiacritics().ToLowerInvariant().Contains(s))
            {
                return true;
            }
            return false;
        }
    }
}