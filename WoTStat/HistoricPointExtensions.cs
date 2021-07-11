using System.Collections.Generic;
using System.Linq;

namespace Negri.Wot
{
    public static class HistoricPointExtensions
    {
        /// <summary>
        /// Retira as datas repetidas e devolve com a mais recente no elemento 0
        /// </summary>
        public static IEnumerable<HistoricPoint> Normalize(this IEnumerable<HistoricPoint> originalHistory)
        {
            if (originalHistory == null)
            {
                return Enumerable.Empty<HistoricPoint>();
            }

            var a = originalHistory.OrderByDescending(h => h.Date).ToArray();
            if (a.Length < 1)
            {
                return Enumerable.Empty<HistoricPoint>();
            }

            var l = new List<HistoricPoint>();

            for (var i = 1; i < a.Length; i++)
            {
                if ((a[i - 1].Date != a[i].Date) && (a[i-1].Active > 0))
                {
                    l.Add(a[i-1]);
                }                
            }
            if (a[a.Length - 1].Active > 0)
            {
                l.Add(a[a.Length - 1]);
            }

            return l.ToArray();
        }
    }
}