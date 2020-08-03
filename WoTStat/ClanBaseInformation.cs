namespace Negri.Wot
{
    public class ClanBaseInformation
    {
        public ClanBaseInformation(long clanId, string clanTag)
        {
            ClanTag = clanTag;
            ClanId = clanId;
        }

        public Platform Platform { get; protected set; } = Platform.Console;

        public string ClanTag { get; protected set; }

        public long ClanId { get; protected set; }

        public override string ToString()
        {
            return $"[{ClanTag}]({ClanId})";
        }
    }
}