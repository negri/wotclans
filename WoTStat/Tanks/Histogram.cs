using System.Collections.Generic;
using System.Linq;

namespace Negri.Wot.Tanks
{
    public class Histogram
    {
        public Histogram() { }

        public Histogram(IEnumerable<Bin> bins)
        {
            _bins.AddRange(bins);
        }

        private readonly List<Bin> _bins = new();

        public void Add(double lower, double upper, long count)
        {
            _bins.Add(new Bin{Lower = lower, Upper = upper, Count = count});
        }

        public Bin[] Bins
        {
            get { return _bins.OrderBy(b => b.Lower).ToArray(); }
            set
            {
                _bins.Clear();
                if (value == null)
                {
                    return;
                }
                _bins.AddRange(value);
            }
        }

        public int Count => _bins.Count;

        /// <summary>
        /// Um bin de um histograma
        /// </summary>
        public class Bin
        {
            public double Lower { get; set; }

            public double Upper { get; set; }

            public double Mid => (Lower + Upper) / 2.0;

            public long Count { get; set; }
        }
    }
}