using System;
using System.Linq;
using Negri.Wot.Tanks;

namespace Negri.Wot.Models
{
    public class TanksWn8
    {
        private readonly Wn8ExpectedValues _ev;

        public TanksWn8(Wn8ExpectedValues ev)
        {
            _ev = ev;
            Tanks = ev.AllTanks.OrderByDescending(t => t.Tier).ThenBy(t => t.Type)
                .ThenBy(t => t.Nation).ThenBy(t => t.Nation).ToArray();
        }

        public Wn8TankExpectedValues[] Tanks { get; }

        public string Source => _ev.Source;

        public string Version => _ev.Version;

        public DateTime Date => _ev.Date;
    }
}