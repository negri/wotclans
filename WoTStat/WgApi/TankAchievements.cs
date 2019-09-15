using System.Collections.Generic;
using Newtonsoft.Json;

namespace Negri.Wot.WgApi
{
    /// <summary>
    /// Achievements in a tank
    /// </summary>
    public class TankAchievements
    {
        [JsonProperty("account_id")]
        public long PlayerId { get; set; }

        [JsonProperty("tank_id")]
        public long TankId { get; set; }

        [JsonProperty("achievements")]
        public Dictionary<string, int> Achievements { get; set; } = new Dictionary<string, int>();

        [JsonProperty("series")]
        public Dictionary<string, int> Series { get; set; } = new Dictionary<string, int>();

        [JsonProperty("max_series")]
        public Dictionary<string, int> MaxSeries { get; set; } = new Dictionary<string, int>();

        [JsonProperty("ribbons")]
        public Dictionary<string, int> Ribbons { get; set; } = new Dictionary<string, int>();
    }
}