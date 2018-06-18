namespace Negri.Wot.Models
{
    public static class ClanExtensions
    {
        public static string IsOldDataClass(this Clan clan)
        {
            return clan.IsOldData ? "old-data" : null;
        }
    }
}