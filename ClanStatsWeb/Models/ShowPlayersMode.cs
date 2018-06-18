using System;

namespace Negri.Wot.Models
{
    public enum ShowPlayersMode
    {
        [Obsolete]
        Top15 = 0,

        [Obsolete]
        AverageAndAbove = 1,

        AllActive = 2,

        All = 3
    }
}