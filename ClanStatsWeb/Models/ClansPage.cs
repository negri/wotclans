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
        ///     Retorna os países com mais clãs
        /// </summary>
        public IEnumerable<string> GetMostCountries(int minNumberOfClans = 2, int maxCountries = 10)
        {
            var countries =
                _clans.Where(c => !string.IsNullOrWhiteSpace(c.Country))
                    .GroupBy(c => c.Country)
                    .Select(g => new Tuple<string, int>(g.Key, g.Count()))
                    .Where(t => t.Item2 >= minNumberOfClans)
                    .OrderByDescending(t => t.Item2)
                    .ThenBy(t => t.Item1)
                    .Take(maxCountries)
                    .Select(t => t.Item1.ToLowerInvariant())
                    .OrderBy(s => s);

            return countries;
        }

    }

    public class ApiClansReturn
    {
        private readonly Clan[] _clans;


        public ApiClansReturn(Clan[] clans, string countryFilter = null, int minActiveSize = 7, int maxActiveSize = 200, int minWn8T15 = 900, int maxWn8T15 = 8000, string clanFilter = null)
        {
            IsAllDataOld = clans.All(c => c.IsObsolete || !c.IsActive);
            _clans = IsAllDataOld ? clans : clans.Where(c => !c.IsObsolete && c.IsActive).ToArray();

            CountryFilter = countryFilter;

            MinActiveSize = minActiveSize;
            MaxActiveSize = maxActiveSize;

            MinWn8T15 = minWn8T15;
            MaxWn8T15 = maxWn8T15;

            ClanFilter = clanFilter;
        }

        public bool IsAllDataOld { get; }

        public int Count => _clans.Length;

        public int Players => _clans.Sum(c => c.Count);

        public DateTime Moment => _clans.Max(c => c.Moment);

        public string CountryFilter { get; }
        public int MaxActiveSize { get; }
        public int MinActiveSize { get; }
        public int MinWn8T15 { get; }
        public int MaxWn8T15 { get; }
        public string ClanFilter { get; }

        public IEnumerable<Clan> Clans
        {
            get
            {
                var clans = _clans.Where(c => !c.IsHidden);

                if (!string.IsNullOrWhiteSpace(CountryFilter))
                {
                    clans =
                        clans.Where(
                            c =>
                                string.Equals(c.Country, CountryFilter,
                                    StringComparison.InvariantCultureIgnoreCase));
                }

                clans = clans.Where(c => c.Active >= MinActiveSize);
                clans = clans.Where(c => c.Active <= MaxActiveSize);
                clans = clans.Where(c => c.Top15Wn8 >= MinWn8T15);
                clans = clans.Where(c => c.Top15Wn8 <= MaxWn8T15);

                var clansWithOrder = clans.OrderByDescending(c => c.Top15Wn8).Select((clan, i) => (i + 1, clan)).ActOnEach(rc => rc.clan.Rank = rc.Item1)
                    .Select(rc => rc.clan);

                // The last, as we need to show the placement according to filters
                if (!string.IsNullOrWhiteSpace(ClanFilter))
                {
                    clansWithOrder = clansWithOrder.Where(c => c.ClanTag.ToUpperInvariant().Contains(ClanFilter.ToUpperInvariant()));
                }

                return clansWithOrder.ToArray();
            }
        }

        
        
        
    }
}