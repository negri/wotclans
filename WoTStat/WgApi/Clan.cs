using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Negri.Wot.WgApi
{
    /// <summary>
    /// Um clã
    /// </summary>
    public class Clan
    {
        /// <summary>
        /// Id do Clã (nunca muda)
        /// </summary>
        [JsonProperty("clan_id")]
        public long ClanId { get; set; }

        /// <summary>
        /// Tag do Clã (as vezes muda)
        /// </summary>
        public string Tag { get; set; }

        /// <summary>
        /// Nome do Clã (muda com frequencia)
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Numero de membros do clã
        /// </summary>
        [JsonProperty("members_count")]
        public int MembersCount { get; set; }

        /// <summary>
        /// Data de criação do clã
        /// </summary>
        [JsonProperty("created_at")]
        public long CreatedAtUnix { get; set; }

        public DateTime CreatedAtUtc => CreatedAtUnix.ToDateTime();

        /// <summary>
        /// Se o clã foi desfeito
        /// </summary>
        [JsonProperty("is_clan_disbanded")]
        public bool IsDisbanded { get; set; }

        /// <summary>
        /// Os membros e seus papeis no clã
        /// </summary>
        public Dictionary<long, Member> Members { get; set; } = new Dictionary<long, Member>();

        [JsonProperty("members_ids")]
        public long[] MembersIds { get; set; } = new long[0];

    }
}