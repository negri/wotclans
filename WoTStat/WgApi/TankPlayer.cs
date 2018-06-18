using System;
using Negri.Wot.Tanks;
using Newtonsoft.Json;

namespace Negri.Wot.WgApi
{
    /// <summary>
    /// Um tanque jogado
    /// </summary>
    public class TankPlayer
    {
        public TankPlayer()
        {
            Moment = DateTime.UtcNow;
        }

        /// <summary>
        /// Momento (UTC) da captura de dados
        /// </summary>
        public DateTime Moment { get; set; }

        /// <summary>
        /// A plataforma
        /// </summary>
        public Plataform Plataform { get; set; }

        /// <summary>
        /// A data a que se referem esses dados (Data da ultima batalha)
        /// </summary>
        public DateTime Date => LastBattle.Date;

        [JsonProperty("account_id")]
        public long PlayerId { get; set; }

        [JsonProperty("tank_id")]
        public long TankId { get; set; }

        [JsonProperty("last_battle_time")]
        public long LastBattleUnix { get; set; }

        /// <summary>
        /// Quando foi feita a ultima batalha
        /// </summary>
        public DateTime LastBattle => LastBattleUnix.ToDateTime();

        [JsonProperty("trees_cut")]
        public long TreesCut { get; set; }

        [JsonProperty("max_frags")]
        public long MaxFrags { get; set; }

        [JsonProperty("mark_of_mastery")]
        public long MarkOfMastery { get; set; }

        [JsonProperty("battle_life_time")]
        public long BattleLifeTimeSeconds { get; set; }

        /// <summary>
        /// Tempo jogado com esse tanque
        /// </summary>
        public TimeSpan BattleLifeTime => TimeSpan.FromSeconds(BattleLifeTimeSeconds);

        /// <summary>
        /// Todos os detalhes de jogadas com esse tanque
        /// </summary>
        [JsonProperty("all")]
        public TankPlayerStatistics All { get; set; }
    }
}