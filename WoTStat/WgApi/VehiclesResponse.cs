using System.Collections.Generic;
using Newtonsoft.Json;

namespace Negri.Wot.WgApi
{
    /// <summary>
    /// A resposta da busca de tanques
    /// </summary>
    public class VehiclesResponse : ResponseBase
    {
        [JsonProperty("data")]
        public Dictionary<long, Tank> Data { get; set; } = new Dictionary<long, Tank>();
    }
}