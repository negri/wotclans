using System.Collections.Generic;
using Newtonsoft.Json;

namespace Negri.Wot.WgApi
{
    /// <summary>
    /// The response for stats on a tank
    /// </summary>
    public class TanksStatsResponse : ResponseBase
    {
        [JsonProperty("data")]
        public Dictionary<long, TankPlayer[]> Players { get; set; } = new Dictionary<long, TankPlayer[]>();
    }
}