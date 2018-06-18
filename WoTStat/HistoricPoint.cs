using System;
using Newtonsoft.Json;

namespace Negri.Wot
{
    public class HistoricPoint
    {
        public HistoricPoint() { }

        public HistoricPoint(Clan clan)
        {
            Moment = clan.Moment;

            Count = clan.Count;
            Active = clan.Active;

            TotalBattles = clan.TotalBattles;
            ActiveBattles = clan.ActiveBattles;

            TotalWinRate = clan.TotalWinRate;
            ActiveWinRate = clan.ActiveWinRate;

            TotalWn8 = clan.TotalWn8;                                    
            ActiveWn8 = clan.ActiveWn8;            
            Top15Wn8 = clan.Top15Wn8;
            Top7Wn8 = clan.Top7Wn8;

            Top15AvgTier = clan.Top15AvgTier;
            ActiveAvgTier = clan.ActiveAvgTier;
            TotalAvgTier = clan.TotalAvgTier;
        }

        public DateTime Date => Moment.Date;

        public DateTime Moment { get; set; }

        public int Count { get; set; }

        public int Active { get; set; }

        public int TotalBattles { get; set; }

        public int ActiveBattles { get; set; }

        public double TotalWinRate { get; set; }

        public double ActiveWinRate { get; set; }
        

        public double ActivityPercent => 1.0 * Active / Count;
        
        public double TotalWn8 { get; set; }
       
        public double ActiveWn8 { get; set; }

        public double Top15Wn8 { get; set; }

        public double Top7Wn8 { get; set; }

        public double Top15AvgTier { get; set; }

        public double ActiveAvgTier { get; set; }

        public double TotalAvgTier { get; set; }

    }
}