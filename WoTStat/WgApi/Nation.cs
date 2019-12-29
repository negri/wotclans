using System.ComponentModel;

namespace Negri.Wot.WgApi
{
    public enum Nation
    {
        [Description("Unknown")]
        Unknown = 0,

        [Description("U.S.A.")]
        Usa = 1,

        [Description("France")]
        France = 2,

        [Description("U.S.S.R")]
        Ussr = 3,

        [Description("China")]
        China = 4,

        [Description("U.K.")]
        Uk = 5,

        [Description("Japan")]
        Japan = 6,

        [Description("Germany")]
        Germany = 7,

        [Description("Czechoslovakia")]
        Czechoslovakia = 8,

        [Description("Sweden")]
        Sweden = 9,

        [Description("Poland")]
        Poland = 10,

        [Description("Mercenaries")]
        Mercenaries = 11,

        [Description("Italy")]
        Italy = 12
    }

}
