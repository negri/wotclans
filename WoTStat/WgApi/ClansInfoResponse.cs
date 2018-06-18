using System.Collections.Generic;
using Newtonsoft.Json;

namespace Negri.Wot.WgApi
{
    

    /// <summary>
    /// A resposta da busca por detalhes de clãs
    /// </summary>
    public class ClansInfoResponse : ResponseBase
    {
        /// <summary>
        /// Id do Clã (nunca muda)
        /// </summary>
        [JsonProperty("data")]
        public Dictionary<long, Clan> Clans { get; set; } = new Dictionary<long, Clan>();
    }
}