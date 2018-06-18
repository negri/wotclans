
namespace Negri.Wot
{
    /// <summary>
    /// As faixas do WN8
    /// </summary>
    /// <remarks>
    /// O valor de cada item é a borda inferior, inclusiva, da faixa
    /// </remarks>
    public enum Wn8Rating
    {
        [RgbColor(147, 13, 13)]
        VeryBad = 0,

        [RgbColor(205, 51, 51)]
        Bad = 300,

        [RgbColor(204, 122, 0)]
        BelowAverage = 450,

        [RgbColor(204, 184, 0)]
        Average = 650,

        [RgbColor(132, 155, 36)]
        AboveAverage = 900,

        [RgbColor(77, 115, 38)]
        Good = 1200,

        [RgbColor(64, 153, 191)]
        VeryGood = 1600,

        [RgbColor(57, 114, 198)]
        Great = 2000,

        [RgbColor(121, 61, 182)]
        Unicum = 2450,

        [RgbColor(64, 16, 112)]
        SuperUnicum = 2900
    }
}