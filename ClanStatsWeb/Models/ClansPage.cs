using System;
using System.Collections.Generic;
using System.Linq;

namespace Negri.Wot.Models
{
    public class ClansPage
    {
        private readonly Clan[] _clans;

        private readonly string _countryFilter;
        private readonly bool _invertCountryFilter;
        private readonly int _maxActiveSize;
        private readonly int _minActiveSize;
        private readonly double _minWn8T15;
        private readonly bool _isAllDataOld;

        public ClansPage(IEnumerable<Clan> clans, string countryFilter = null, bool invertCountryFilter = false, int minActiveSize = 0, int maxActiveSize = 200,
            int minWn8T15 = 900)
        {
            _countryFilter = countryFilter;
            _invertCountryFilter = invertCountryFilter;
            _minActiveSize = minActiveSize;
            _maxActiveSize = maxActiveSize;
            _minWn8T15 = minWn8T15;
            _clans = clans.ToArray();

            if (_clans.Count(c => !c.IsObsolete) <= 0)
            {
                // Está tudo velho... ou meu site morreu, ou é dev e não tem arquivos para atualizar... mostra o que tem
                _isAllDataOld = true;
            }

        }

        public int Count => _clans.Length;

        public int Players => _clans.Where(c => _isAllDataOld || !c.IsObsolete).Sum(c => c.Count);

        public DateTime Moment => _clans.Max(c => c.Moment);

        public IEnumerable<Tuple<int, Clan>> Clans
        {
            get
            {
                var clans = (Tournament == null)
                    ? _clans.Where(c => c.IsActive && (!c.IsObsolete || _isAllDataOld) && !c.IsHidden)
                    : _clans.AsEnumerable();

                if (!string.IsNullOrWhiteSpace(_countryFilter))
                {
                    if (_invertCountryFilter)
                    {
                        clans =
                            clans.Where(
                                c =>
                                    !string.Equals(c.Country, _countryFilter,
                                        StringComparison.InvariantCultureIgnoreCase));
                    }
                    else
                    {
                        clans =
                            clans.Where(
                                c =>
                                    string.Equals(c.Country, _countryFilter,
                                        StringComparison.InvariantCultureIgnoreCase));
                    }
                }

                clans = clans.Where(c => c.Active >= _minActiveSize);
                clans = clans.Where(c => c.Active <= _maxActiveSize);
                clans = clans.Where(c => c.Top15Wn8 >= _minWn8T15);

                var orderedClans = clans.OrderByDescending(c => c.Top15Wn8).Select((c, i) => new Tuple<int, Clan>(i + 1, c)).ToArray();

                if (Tournament != null)
                {
                    var notListed = new HashSet<string>(Tournament.Clans);
                    notListed.ExceptWith(orderedClans.Select(o => o.Item2.ClanTag));
                    NotListedOnTournamentCount = notListed.Count;
                }

                return orderedClans;
            }
        }

        public int NotListedOnTournamentCount { get; set; }

        /// <summary>
        /// O Torneio
        /// </summary>
        public Tournament Tournament { get; set; }

        /// <summary>
        /// Tournament description.
        /// </summary>
        public string TournamentDescription => Tournament?.Description ?? string.Empty;

        /// <summary>
        ///     Retorna os paises com mais clãs
        /// </summary>
        public IEnumerable<string> GetMostCountries(int minNumberOfClans = 2, int maxCountries = 10)
        {
            var countries =
                _clans.Where(c => (!c.IsObsolete || _isAllDataOld) && !string.IsNullOrWhiteSpace(c.Country) && c.IsActive)
                    .GroupBy(c => c.Country)
                    .Select(g => new Tuple<string, int>(g.Key, g.Count()))
                    .Where(t => t.Item2 >= minNumberOfClans)
                    .OrderByDescending(t => t.Item2)
                    .ThenBy(t => t.Item1)
                    .Take(maxCountries)
                    .Select(t => t.Item1.ToUpperInvariant())
                    .OrderBy(s => s);

            return countries;
        }
    }
}