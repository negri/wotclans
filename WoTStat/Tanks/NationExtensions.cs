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
    }

}
