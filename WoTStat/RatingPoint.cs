using System.Collections.Generic;
using System.Linq;

namespace Negri.Wot
{
    public class RatingPoint
    {
        public Wn8Rating Wn8Rating { get; set; }

        /// <summary>
        /// Jogadores
        /// </summary>        
        public int Count { get; set; }

        /// <summary>
        /// Porcentagem de Jogadores
        /// </summary>        
        public double Percent { get; set; }

        /// <summary>
        /// Batalhas de Jogadores
        /// </summary>        
        public int BattlesCount { get; set; }

        /// <summary>
        /// Porcentagem de Batalhas de Jogadores
        /// </summary>        
        public double BattlesPercent { get; set; }

        public static IEnumerable<RatingPoint> GetDistribution(IEnumerable<Player> players)
        {
            var histogram = new Dictionary<Wn8Rating, RatingPoint>();
            
            var total = 0;
            var totalBattles = 0;
            foreach (var player in players)
            {
                total++;
                totalBattles += player.MonthBattles;

                var wn8 = player.MonthWn8.GetRating();
                RatingPoint ratingPoint;
                if (histogram.TryGetValue(wn8, out ratingPoint))
                {
                    ratingPoint.Count++;
                    ratingPoint.BattlesCount += player.MonthBattles;
                }
                else
                {
                    ratingPoint = new RatingPoint { Count = 1, Wn8Rating = wn8, BattlesCount = player.MonthBattles};
                    histogram.Add(wn8, ratingPoint);
                }
            }

            // Coloca os pontos faltantes
            if (!histogram.ContainsKey(Wn8Rating.VeryBad)) histogram.Add(Wn8Rating.VeryBad, new RatingPoint {Wn8Rating = Wn8Rating.VeryBad});
            if (!histogram.ContainsKey(Wn8Rating.Bad)) histogram.Add(Wn8Rating.Bad, new RatingPoint { Wn8Rating = Wn8Rating.Bad });
            if (!histogram.ContainsKey(Wn8Rating.BelowAverage)) histogram.Add(Wn8Rating.BelowAverage, new RatingPoint { Wn8Rating = Wn8Rating.BelowAverage });
            if (!histogram.ContainsKey(Wn8Rating.Average)) histogram.Add(Wn8Rating.Average, new RatingPoint { Wn8Rating = Wn8Rating.Average });
            if (!histogram.ContainsKey(Wn8Rating.Good)) histogram.Add(Wn8Rating.Good, new RatingPoint { Wn8Rating = Wn8Rating.Good });
            if (!histogram.ContainsKey(Wn8Rating.VeryGood)) histogram.Add(Wn8Rating.VeryGood, new RatingPoint { Wn8Rating = Wn8Rating.VeryGood });
            if (!histogram.ContainsKey(Wn8Rating.Great)) histogram.Add(Wn8Rating.Great, new RatingPoint { Wn8Rating = Wn8Rating.Great });
            if (!histogram.ContainsKey(Wn8Rating.Unicum)) histogram.Add(Wn8Rating.Unicum, new RatingPoint { Wn8Rating = Wn8Rating.Unicum });
            if (!histogram.ContainsKey(Wn8Rating.SuperUnicum)) histogram.Add(Wn8Rating.SuperUnicum, new RatingPoint { Wn8Rating = Wn8Rating.SuperUnicum });

            foreach (var value in histogram.Values)
            {
                value.Percent = value.Count / (double)total;
                value.BattlesPercent = value.BattlesCount/(double) totalBattles;
            }

            return histogram.Values.OrderBy(rp => (int)rp.Wn8Rating);
        }
    }

    
}