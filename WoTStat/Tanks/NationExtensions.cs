using System.Collections.Generic;
using Negri.Wot.WgApi;

namespace Negri.Wot.Tanks
{
    public static class NationExtensions
    {
        public static string ToStringUrl(this Nation nation)
        {
            if (nation == Nation.Czechoslovakia)
            {
                return "czech";
            }
            if (nation == Nation.Mercenaries)
            {
                return "merc";
            }
            return nation.ToString().ToLowerInvariant();
        }

        public static IEnumerable<Nation> GetGameNations()
        {
            yield return Nation.Usa;
            yield return Nation.France;
            yield return Nation.Ussr;
            yield return Nation.China;
            yield return Nation.Uk;
            yield return Nation.Japan;
            yield return Nation.Germany;
            yield return Nation.Czechoslovakia;
            yield return Nation.Sweden;
            yield return Nation.Poland;
            yield return Nation.Mercenaries;
            yield return Nation.Italy;
        }
    }

}
