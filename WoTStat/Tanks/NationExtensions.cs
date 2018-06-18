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
            return nation.ToString().ToLowerInvariant();
        }
    }

}
