using System.Collections.Generic;
using Newtonsoft.Json;

namespace Negri.Wot.WgApi
{
    /// <summary>
    ///     Um tanque do jogo
    /// </summary>
    public class Tank
    {
        /// <summary>
        /// A plataforma do tanque
        /// </summary>
        public Plataform Plataform { get; set; }

        /// <summary>
        ///     Id do tanque.
        /// </summary>
        [JsonProperty("tank_id")]
        public long TankId { get; set; }

        /// <summary>
        ///     Nome (maior) do tanque
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        ///     Nome (curto) do tanque
        /// </summary>
        [JsonProperty("short_name")]
        public string ShortName { get; set; }

        /// <summary>
        ///     Nação
        /// </summary>
        [JsonProperty("nation")]
        public string NationString { get; set; }

        public Nation Nation
        {
            get
            {
                if (string.IsNullOrWhiteSpace(NationString))
                {
                    return Nation.Unknown;
                }
                switch (NationString)
                {
                    case "usa": return Nation.Usa;
                    case "france": return Nation.France;
                    case "ussr": return Nation.Ussr;
                    case "china": return Nation.China;
                    case "uk": return Nation.Uk;
                    case "japan": return Nation.Japan;
                    case "germany": return Nation.Germany;
                    case "czech": return Nation.Czechoslovakia;
                    case "sweden": return Nation.Sweden;
                    case "poland": return Nation.Poland;
                    default:
                        return Nation.Unknown;
                }
            }
        }

        /// <summary>
        ///     Se é premium
        /// </summary>
        [JsonProperty("is_premium")]
        public bool IsPremium { get; set; }

        /// <summary>
        ///     Nivel
        /// </summary>
        public int Tier { get; set; }

        /// <summary>
        ///     Tag do tanque
        /// </summary>
        public string Tag { get; set; }

        /// <summary>
        ///     Tipo do tanque
        /// </summary>
        [JsonProperty("type")]
        public string TypeString { get; set; }

        public TankType Type
        {
            get
            {
                if (string.IsNullOrWhiteSpace(TypeString))
                {
                    return TankType.Unknown;
                }

                switch (TypeString)
                {
                    case "heavyTank":  return TankType.Heavy;
                    case "AT-SPG":     return TankType.TankDestroyer;
                    case "mediumTank": return TankType.Medium;
                    case "lightTank":  return TankType.Light;
                    case "SPG":        return TankType.Artillery;
                    default:
                        return TankType.Unknown;
                }
            }
        }

        /// <summary>
        ///     Imagens para o tanque
        /// </summary>
        [JsonProperty("images")]
        public Dictionary<string, string> Images { get; set; } = new Dictionary<string, string>();
    }
}