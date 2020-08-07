using System;
using Negri.Wot.WgApi;

namespace Negri.Wot.Tanks
{
    public class TankReferenceBasic : Tank
    {
        /// <summary>
        /// Tanques Espotados por Partida
        /// </summary>
        public double Spotted { get; set; }

        public double DamageAssistedTrack { get; set; }

        public double DamageAssistedRadio { get; set; }

        public double DamageAssisted => DamageAssistedTrack + DamageAssistedRadio;

        public double CapturePoints { get; set; }

        public double DroppedCapturePoints { get; set; }

        public long TotalBattles { get; set; }

        public long TotalPlayers { get; set; }

        public double BattlesPerPlayer => TotalBattles * 1.0 / TotalPlayers;

        public long TotalWins { get; set; }

        public long TotalLosses { get; set; }

        public long TotalDraws => TotalBattles - TotalWins - TotalLosses;

        /// <summary>
        /// Inimigos mortos, Kills
        /// </summary>
        public double Kills { get; set; }

        public long TotalSurvivedBattles { get; set; }

        public double SurvivalRate => TotalSurvivedBattles * 1.0 / TotalBattles;

        public long TotalDeaths => TotalBattles - TotalSurvivedBattles;

        public double KillDeathRatio => Kills * TotalBattles / TotalDeaths;

        public double DamageDealt { get; set; }

        public double DamageReceived { get; set; }

        public double DamageRatio => DamageDealt / DamageReceived;

        public double WinRatio => TotalWins * 1.0 / TotalBattles;

        public double TotalDamage => DamageDealt + DamageAssisted;

        public double TotalDamageRatio => TotalDamage / DamageReceived;

        public double TreesCut { get; set; }

        /// <summary>
        /// Média de Max Frags
        /// </summary>
        public double MaxKills { get; set; }

        /// <summary>
        /// Média das marcas
        /// </summary>
        public double MarkOfMastery { get; set; }

        /// <summary>
        /// Acertos
        /// </summary>
        public double Hits { get; set; }

        /// <summary>
        /// Tiros disparados
        /// </summary>
        public double Shots { get; set; }

        /// <summary>
        /// Tiros disparados que acertaram
        /// </summary>
        public double HitRatio => Hits / Shots;

        /// <summary>
        /// Tiros que penetraram
        /// </summary>
        public double Piercings { get; set; }

        /// <summary>
        /// Taxa de Penetração, dado um acerto, se entra ou não
        /// </summary>
        public double PiercingRatio => Piercings / Hits;

        /// <summary>
        /// Taxa de Penetração, dado um disparo
        /// </summary>
        public double PiercingsToShotsRatio => Piercings / Shots;

        /// <summary>
        /// Tiros explosivos acertados
        /// </summary>
        public double ExplosionHits { get; set; }

        /// <summary>
        /// Tiros diretos recebidos
        /// </summary>
        public double DirectHitsReceived { get; set; }

        /// <summary>
        /// Penetrações recebidas
        /// </summary>
        public double PiercingReceived { get; set; }

        /// <summary>
        /// Tiros bloqueados
        /// </summary>
        public double BlockedRatio => NoDamageDirectHitsReceived / DirectHitsReceived;

        public double NoDamageDirectHitsReceived { get; set; }

        public double ExplosionHitsReceived { get; set; }

        /// <summary>
        /// Dano por penetração
        /// </summary>
        public double DamagePerEffectiveHit
        {
            get
            {
                if (Type == TankType.Artillery)
                {
                    return DamageDealt / ExplosionHits;
                }

                return DamageDealt / Piercings;
            }
        }

        /// <summary>
        /// XP por partida
        /// </summary>
        public double XP { get; set; }

        /// <summary>
        /// Tempo total em que o tanque foi jogado
        /// </summary>
        public TimeSpan TotalTime { get; set; }

        public TimeSpan TimePerPlayer => TimeSpan.FromHours(TotalTime.TotalHours / TotalPlayers);

        public TimeSpan TimePerBattle => TimeSpan.FromHours(TotalTime.TotalHours / TotalBattles);

        public double DamageDealtPerMinute => DamageDealt / TimePerBattle.TotalMinutes;

        public double TotalDamagePerMinute => TotalDamage / TimePerBattle.TotalMinutes;

        public double XPPerMinute => XP / TimePerBattle.TotalMinutes;

        /// <summary>
        /// O WN8 médio com o tanque no servidor
        /// </summary>
        public double AverageWn8 { get; set; }

    }
}