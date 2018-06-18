using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Negri.Wot.Bot
{
    public static class Features
    {
        public const string Games = "Games";
        public const string Clans = "Clans";
        public const string Players = "Players";
        public const string Tanks = "Tanks";
        public const string General = "General";

        public static bool IsFeature(this string feature)
        {
            foreach(var f in GetAll())
            {
                if (f == feature)
                {
                    return true;
                }
            }
            return false;
        }

        public static IEnumerable<string> GetAll()
        {
            return typeof(Features).GetAllPublicConstantValues<string>();
        }
    }
}