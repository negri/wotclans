using System;
using System.Collections.Generic;
using System.Linq;
using Negri.Wot.Tanks;

namespace Negri.Wot.Models
{
    public class TanksMoe
    {
        public TanksMoe(IDictionary<long, TankMoe> moes)
        {
            Tanks = moes.Values.Where(t => t.Tier >= 5).OrderByDescending(t => t.Tier).ThenBy(t => t.Type)
                .ThenBy(t => t.Nation).ThenBy(t => t.Nation).ToArray();
            Date = moes.First().Value.Date;
        }

        public TankMoe[] Tanks { get; }

        public DateTime Date { get; }

        /// <summary>
        ///     O dia anteriors
        /// </summary>
        public DateTime Date1D => Date.AddDays(-1);

        /// <summary>
        ///     A semana anterior
        /// </summary>
        public DateTime Date1W => Date.AddDays(-7);

        /// <summary>
        ///     O mês anterior
        /// </summary>
        public DateTime Date1M => Date.AddDays(-28);
    }
}