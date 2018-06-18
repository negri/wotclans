using System.Diagnostics.CodeAnalysis;

namespace Negri.Wot
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class ClanSummary : ClanPlataform
    {
        public ClanSummary(Clan c) : base(c.Plataform, c.ClanId, c.ClanTag)
        {
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

        public string Name { get; set; }

        public double TotalWn8 { get; set; }

        public double TotalWinRate { get; set; }

        public int TotalBattles { get; set; }

        public int TotalPlayers { get; set; }

        public double WN8t7 { get; set; }

        public double WN8t15 { get; set; }

        public double WN8a { get; set; }

        public double ActiveWinRate { get; set; }

        public int ActiveBattles { get; set; }

        public int ActivePlayers { get; set; }

        public string Country { get; set; }
    }
}