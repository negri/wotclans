using System;
using System.Collections.Generic;
using System.Linq;
using Negri.Wot.Tanks;

namespace Negri.Wot.Models
{
    public class ClanPage
    {
        public Clan Clan { get; set; }

        public IEnumerable<Tuple<int, Player>> Players
        {
            get
            {
                IEnumerable<Player> players;

                switch (ShowPlayersMode)
                {
                    case ShowPlayersMode.All:
                        players = Clan.Players;
                        break;
                    default:
                        players = Clan.ActivePlayers;
                        break;
                }

                var playersArray = players.OrderByDescending(p => p.IsActive).ThenByDescending(p => p.MonthWn8).ToArray();

                HasBadPlayers = playersArray.Any(p => p.MonthWn8 < (int) Wn8Rating.BelowAverage);
                HasBadPlayersOnTop15 = playersArray.Take(15).Any(p => p.MonthWn8 < (int)Wn8Rating.BelowAverage);

                return playersArray.Select(((player, i) => new Tuple<int, Player>(i + 1, player)));
            }
        }
        
        public bool HasBadPlayers { get; private set; }

        public bool HasBadPlayersOnTop15 { get; private set; }

        public ShowPlayersMode ShowPlayersMode { get; set; }

        /// <summary>
        /// Historico de WN8
        /// </summary>
        public IEnumerable<HistoricPoint> History => Clan?.History;

        public IEnumerable<RatingPoint> RatingDistribution => RatingPoint.GetDistribution(Clan.ActivePlayers);

        /// <summary>
        /// Lideres nos tanques
        /// </summary>
        public Leader[] Leaders { get; set; } = new Leader[0];

        public bool HasLeaders => Leaders.Length > 0;
    }
}