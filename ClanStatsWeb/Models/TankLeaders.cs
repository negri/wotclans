using System;
using System.Linq;
using Negri.Wot.Tanks;

namespace Negri.Wot.Models
{
    /// <summary>
    /// Lideres nos tanques
    /// </summary>
    public class TankLeaders
    {
        public DateTime Date => Leaders.FirstOrDefault()?.Date ?? DateTime.MinValue;

        public int TotalLeaders { get; set; }

        public int Returned => Leaders.Length;

        public Leader[] Leaders { get; set; } = new Leader[0];
    }
}