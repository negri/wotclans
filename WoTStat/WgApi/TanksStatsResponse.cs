using System.Collections.Generic;
using Newtonsoft.Json;

namespace Negri.Wot.WgApi
{
    /// <summary>
    /// A resposta da busca por estatisticas de jogadores em seus tanques
    /// </summary>
    public class TanksStatsResponse : ResponseBase
    {
        /// <summary>
        /// Id do Clã (nunca muda)
        /// </summary>
        [JsonProperty("data")]
        public Dictionary<long, TankPlayer[]> Tanks { get; set; } = new Dictionary<long, TankPlayer[]>();
    }
}