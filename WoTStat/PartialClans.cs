using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Negri.Wot
{
    /// <summary>
    ///     Conjuntos de clãs parciais (na verdade um conjunto de jogadores com nome)
    /// </summary>
    public class PartialClans
    {
        private Clan[] _clans;
        public string Tag { get; set; }

        public Player[] Players { get; set; }

        public Clan[] Clans => _clans ?? new Clan[0];

        [JsonIgnore]
        public DateTime Moment
        {
            get { return Players.Select(p => p.Moment).Max(); }
        }

        public static PartialClans FromFile(string baseStoreDir, string tag)
        {
            var json = File.ReadAllText(GetFilePath(baseStoreDir, tag), Encoding.UTF8);
            return JsonConvert.DeserializeObject<PartialClans>(json);
        }

        private static string GetFilePath(string baseStoreDir, string tag)
        {
            var dir = Path.Combine(baseStoreDir, "PartialClans");
            var file = Path.Combine(dir, $"PartialClan.{tag}.json");
            return file;
        }

        public static bool Exists(string baseStoreDir, string tag)
        {
            return File.Exists(GetFilePath(baseStoreDir, tag));
        }

        public void Save(string baseStoreDir)
        {
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(GetFilePath(baseStoreDir, Tag), json, Encoding.UTF8);
        }

        /// <summary>
        ///     Calcula os clãs parciais existentes
        /// </summary>
        public void Calculate()
        {
            if (_clans != null)
            {
                return;
            }
            var clans = new Dictionary<string, Clan>();

            foreach (var player in Players)
            {
                Clan clan;
                if (!clans.TryGetValue(player.ClanTag, out clan))
                {
                    clan = new Clan(player.Plataform, 0, player.ClanTag, $"{player.ClanTag} - Partial {Tag}");
                    clans.Add(clan.ClanTag, clan);
                }
                clan.Add(player);
            }

            _clans = clans.Values.OrderByDescending(c => c.MonthWn8).ToArray();
        }
    }
}