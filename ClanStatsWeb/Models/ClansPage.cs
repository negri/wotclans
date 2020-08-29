using System;
using System.Collections.Generic;
using System.Linq;

namespace Negri.Wot.Models
{
    public class ClansPage
    {
        private readonly Clan[] _clans;

        public ClansPage(Clan[] clans)
        {
            IsAllDataOld = clans.All(c => c.IsObsolete || !c.IsActive);

            _clans = IsAllDataOld ? clans : clans.Where(c => !c.IsObsolete && c.IsActive).ToArray();

            Count = _clans.Length;
            Players = _clans.Sum(c => c.Count);
            Moment = _clans.Max(c => c.Moment);
        }

        public bool IsAllDataOld { get; }

        public int Count { get; }

        public int Players { get; }

        public DateTime Moment { get; }

        /// <summary>
        ///     Return the flags with most players0
        /// </summary>
        public IEnumerable<string> GetMostCountries(int minNumberOfClans = 2, int maxCountries = 10)
        {
            var countries =
                _clans.Where(c => !string.IsNullOrWhiteSpace(c.Country))
                    .GroupBy(c => c.Country)
                    .Select(g => (Country: g.Key, Players: g.Sum(c => c.Active), Clans: g.Count()))
                    .Where(t => t.Clans >= minNumberOfClans)
                    .OrderByDescending(t => t.Players)
                    .ThenBy(t => t.Clans)
                    .Take(maxCountries)
                    .Select(t => t.Country.ToLowerInvariant())
                    .OrderBy(s => s);

            return countries;
        }

    }
}