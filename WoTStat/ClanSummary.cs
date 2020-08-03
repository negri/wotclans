using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace Negri.Wot
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [DataContract]
    public class ClanSummary
    {
        public ClanSummary(Clan c)
        {
            Plataform = c.Platform;
            ClanTag = c.ClanTag;
            ClanId = c.ClanId;
            Country = c.Country;
            ActivePlayers = c.Active;
            ActiveBattles = c.ActiveBattles;
            ActiveWinRate = c.ActiveWinRate;
            WN8a = c.ActiveWn8;
            WN8t15 = c.Top15Wn8;
            WN8t7 = c.Top7Wn8;
            TotalPlayers = c.Count;
            TotalBattles = c.TotalBattles;
            TotalWinRate = c.TotalWinRate;
            TotalWn8 = c.TotalWn8;
            Name = c.Name;
        }

        [DataMember]
        public Platform Plataform { get; protected set; }

        [DataMember]
        public string ClanTag { get; protected set; }

        [DataMember]
        public long ClanId { get; protected set; }

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public double TotalWn8 { get; set; }

        [DataMember]
        public double TotalWinRate { get; set; }

        [DataMember]
        public int TotalBattles { get; set; }

        [DataMember]
        public int TotalPlayers { get; set; }

        [DataMember]
        public double WN8t7 { get; set; }

        [DataMember]
        public double WN8t15 { get; set; }

        [DataMember]
        public double WN8a { get; set; }

        [DataMember]
        public double ActiveWinRate { get; set; }

        [DataMember]
        public int ActiveBattles { get; set; }

        [DataMember]
        public int ActivePlayers { get; set; }

        [DataMember]
        public string Country { get; set; }
    }
}