using Newtonsoft.Json;

namespace Negri.Wot.WgApi
{
    /// <summary>
    /// Um membro de clã
    /// </summary>
    public class Member
    {
        [JsonProperty("account_id")]
        public long PlayerId { get; set; }

        [JsonProperty("account_name")]
        public string Name { get; set; }

        /// <summary>
        /// Papel no clã (Rank)
        /// </summary>
        public string Role { get; set; }

        public Rank Rank
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Role))
                {
                    return Rank.Private;
                }

                switch (Role)
                {
                    case "commander":
                        return Rank.Commander;
                    case "executive_officer":
                        return Rank.ExecutiveOfficer;
                    case "recruitment_officer":
                        return Rank.RecruitmentOfficer;
                    default:
                        return Rank.Private;
                }
            }
        }
    }
}