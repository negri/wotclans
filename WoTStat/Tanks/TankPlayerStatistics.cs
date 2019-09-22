using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Negri.Wot.Tanks
{
    /// <summary>
    /// Estatísticas de um tanque jogado
    /// </summary>
    /// <remarks>
    /// Dados que vem da API da WG
    /// </remarks>
    public class TankPlayerStatistics : Tank
    {
        [JsonProperty("battles")]
        public long Battles { get; set; }

        [JsonProperty("damage_dealt")]
        public long DamageDealt { get; set; }

        [JsonProperty("wins")]
        public long Wins { get; set; }

        /// <summary>
        /// Numero de Enemies killed
        /// </summary>
        [JsonProperty("frags")]
        public long Kills { get; set; }

        [JsonProperty("spotted")]
        public long Spotted { get; set; }

        [JsonProperty("dropped_capture_points")]
        public long DroppedCapturePoints { get; set; }

        /// <summary>
        /// The calculated WN8 on the tank
        /// </summary>
        public double Wn8 { get; set; }

        /// <summary>
        /// Numero de Tiros que penetrou a blindagem
        /// </summary>
        [JsonProperty("piercings_received")]
        public long PiercingsReceived { get; set; }

        /// <summary>
        /// Número de Acertos nos Tanques inimigos
        /// </summary>
        [JsonProperty("hits")]
        public long Hits { get; set; }

        [JsonProperty("damage_assisted_track")]
        public long DamageAssistedTrack { get; set; }

        [JsonProperty("losses")]
        public long Losses { get; set; }

        /// <summary>
        /// Tiros Recebidos que não causaram dano
        /// </summary>
        [JsonProperty("no_damage_direct_hits_received")]
        public long NoDamageDirectHitsReceived { get; set; }

        [JsonProperty("capture_points")]
        public long CapturePoints { get; set; }

        /// <summary>
        /// Dano de Splash causado em inimigos
        /// </summary>
        [JsonProperty("explosion_hits")]
        public long ExplosionHits { get; set; }

        [JsonProperty("damage_received")]
        public long DamageReceived { get; set; }

        /// <summary>
        /// Tiros dados que penetraram
        /// </summary>
        [JsonProperty("piercings")]
        public long Piercings { get; set; }

        /// <summary>
        /// Tiros dados
        /// </summary>
        [JsonProperty("shots")]
        public long Shots { get; set; }

        /// <summary>
        /// Tiros recebidos que causaram dano de splash
        /// </summary>
        [JsonProperty("explosion_hits_received")]
        public long ExplosionHitsReceived { get; set; }

        /// <summary>
        /// Dano de assistência por Radio
        /// </summary>
        [JsonProperty("damage_assisted_radio")]
        public long DamageAssistedRadio { get; set; }

        /// <summary>
        /// XP Base acumulado
        /// </summary>
        [JsonProperty("xp")]
        public long XP { get; set; }

        /// <summary>
        /// Tiros Diretos recebidos
        /// </summary>
        [JsonProperty("direct_hits_received")]
        public long DirectHitsReceived { get; set; }


        [JsonProperty("survived_battles")]
        public long SurvivedBattles { get; set; }

        /// <summary>
        /// Vezes em que morreu na batalha
        /// </summary>
        public long Deaths => Battles - SurvivedBattles;

        /// <summary>
        /// O dano assistido total
        /// </summary>
        public long DamageAssisted => DamageAssistedRadio + DamageAssistedTrack;

        /// <summary>
        /// Dano total causado (para MoE)
        /// </summary>
        public long TotalDamage => DamageAssisted + DamageDealt;

        /// <summary>
        /// Empates
        /// </summary>
        public long Draws => Battles - Wins - Losses;

        /// <summary>
        /// UTC moment of the last battle
        /// </summary>
        public DateTime LastBattle { get; set; }

        /// <summary>
        /// Uprooted tress
        /// </summary>
        public long TreesCut { get; set; }

        /// <summary>
        /// Max Kills
        /// </summary>
        public long MaxFrags { get; set; }

        /// <summary>
        /// Mastery Level
        /// </summary>
        public long MarkOfMastery { get; set; }

        /// <summary>
        /// Seconds in battle
        /// </summary>
        public long BattleLifeTimeSeconds { get; set; }

        /// <summary>
        /// Time in battles
        /// </summary>
        public TimeSpan BattleLifeTime => TimeSpan.FromSeconds(BattleLifeTimeSeconds);

        /// <summary>
        /// Time of the previous battle, when this refers to a Delta
        /// </summary>
        public DateTime? PreviousLastBattle { get; set; }

        /// <summary>
        /// Total Damage per Battle
        /// </summary>
        public double TotalDamagePerBattle => TotalDamage / (double)Battles;


        public TimeSpan LastBattleAge => (DateTime.UtcNow - LastBattle);

        /// <summary>
        /// Direct Damage per Battle
        /// </summary>
        public double DirectDamagePerBattle => DamageDealt / (double) Battles;

        /// <summary>
        /// Damage Assisted per Battle
        /// </summary>
        public double DamageAssistedPerBattle => DamageAssisted / (double)Battles;

        /// <summary>
        /// Kills per battle
        /// </summary>
        public double KillsPerBattle => Kills/ (double)Battles;

        [JsonIgnore]
        public double WinRate => 1.0 * Wins / Battles;

        /// <summary>
        /// XP per Battle
        /// </summary>
        /// <remarks>
        /// As of 2018-06-21 the WG API accumulattes the base XP including the Premium Time multiplier. 
        /// So, this number is only reliable to people that only plays with Premium or only plays withot Premium.
        /// </remarks>
        [JsonIgnore]
        public double XPPerBattle => 1.0 * XP / Battles;

        /// <summary>
        /// XP/h
        /// </summary>
        [JsonIgnore]
        public double XPPerHour => 1.0 * XP / BattleLifeTime.TotalHours;

        /// <summary>
        /// Ribbons on the tank
        /// </summary>
        public Dictionary<string, int> Ribbons { get; set; }

        /// <summary>
        /// Achievements (medals) on the tank
        /// </summary>
        public Dictionary<string, int> Achievements { get; set; }
    }
}