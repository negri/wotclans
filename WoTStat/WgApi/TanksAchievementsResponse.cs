using System.Collections.Generic;
using Newtonsoft.Json;

namespace Negri.Wot.WgApi
{
    /// <summary>
    /// The response for achievements on a tank
    /// </summary>
    public class TanksAchievementsResponse : ResponseBase
    {
        [JsonProperty("data")]
        public Dictionary<long, TankAchievements[]> Players { get; set; } = new Dictionary<long, TankAchievements[]>();
    }
}