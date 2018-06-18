namespace Negri.Wot
{
    public class ClanPlataform
    {
        public ClanPlataform(Plataform plataform, long clanId, string clanTag)
        {
            Plataform = plataform;
            ClanTag = clanTag;
            ClanId = clanId;
        }

        public Plataform Plataform { get; protected set; }

        public string ClanTag { get; protected set; }

        /// <summary>
        /// Id numérico do clã
        /// </summary>
        public long ClanId { get; protected set; }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>
        /// A string that represents the current object.
        /// </returns>
        public override string ToString()
        {
            return $"{ClanTag}@{Plataform}({ClanId})";
        }
    }

}