using System;
using System.IO;
using System.Text;
using Negri.Wot.WgApi;
using Newtonsoft.Json;

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

    /// <summary>
    /// Valores de Referência, calculados para um tanque
    /// </summary>
    public class TankReference : TankReferenceBasic
    {
        /// <summary>
        ///     Data do cálculo
        /// </summary>
        public DateTime Date { get; set; }
                
        /// <summary>
        /// Informações da marca de excelência
        /// </summary>
        public double MoeHighMark { get; set; }

        public double MoE1Mark => MoeHighMark * 0.65;
        public double MoE2Mark => MoeHighMark * 0.85;
        public double MoE3Mark => MoeHighMark * 0.95;

        #region Histograms

        public Histogram KillsHistogram { get; set; }

        public Histogram DamageDealtHistogram { get; set; }

        public Histogram SpottedHistogram { get; set; }

        public Histogram DroppedCapturePointsHistogram { get; set; }

        public Histogram DamageAssistedTrackHistogram { get; set; }

        public Histogram DamageAssistedRadioHistogram { get; set; }

        public Histogram DamageAssistedHistogram { get; set; }

        public Histogram TotalDamageHistogram { get; set; }

        public Histogram WinRatioHistogram { get; set; }

        #endregion

        /// <summary>
        /// The leaders on this tank
        /// </summary>
        public Leader[] Leaders { get; set; } = new Leader[0];

        #region WN8

        /// <summary>
        /// Os valores de esperados de WN8 para o tanque
        /// </summary>
        public Wn8TankExpectedValues Wn8Values { get; set; }
        
        /// <summary>
        /// The damage required to achieve a <see cref="Wn8Rating"/> of Average.
        /// </summary>
        public double TargetDamageAverage { get; set; }

        /// <summary>
        /// Piercing to achieve the Target Damage of Average WN8
        /// </summary>
        public int TargetDamageAveragePiercings => (int) Math.Ceiling(TargetDamageAverage / (LastMonth?.DamagePerEffectiveHit ?? DamagePerEffectiveHit));

        /// <summary>
        /// Shoots to achieve the Target Damage of Average WN8
        /// </summary>
        public int TargetDamageAverageShots => (int)Math.Ceiling(TargetDamageAverage / (LastMonth?.DamagePerEffectiveHit ?? DamagePerEffectiveHit) / (LastMonth?.PiercingsToShotsRatio ?? PiercingsToShotsRatio));

        /// <summary>
        /// The damage required to achieve a <see cref="Wn8Rating"/> of Goof.
        /// </summary>
        public double TargetDamageGood { get; set; }

        /// <summary>
        /// Piercing to achieve the Target Damage of Good WN8
        /// </summary>
        public int TargetDamageGoodPiercings => (int)Math.Ceiling(TargetDamageGood / (LastMonth?.DamagePerEffectiveHit ?? DamagePerEffectiveHit));

        /// <summary>
        /// Shoots to achieve the Target Damage of Good WN8
        /// </summary>
        public int TargetDamageGoodShots => (int)Math.Ceiling(TargetDamageGood / (LastMonth?.DamagePerEffectiveHit ?? DamagePerEffectiveHit) / (LastMonth?.PiercingsToShotsRatio ?? PiercingsToShotsRatio));


        /// <summary>
        /// The damage required to achieve a <see cref="Wn8Rating"/> of Great.
        /// </summary>
        public double TargetDamageGreat { get; set; }

        /// <summary>
        /// Piercing to achieve the Target Damage of Great WN8
        /// </summary>
        public int TargetDamageGreatPiercings => (int)Math.Ceiling(TargetDamageGreat / (LastMonth?.DamagePerEffectiveHit ?? DamagePerEffectiveHit));

        /// <summary>
        /// Shoots to achieve the Target Damage of Good WN8
        /// </summary>
        public int TargetDamageGreatShots => (int)Math.Ceiling(TargetDamageGreat / (LastMonth?.DamagePerEffectiveHit ?? DamagePerEffectiveHit) / (LastMonth?.PiercingsToShotsRatio ?? PiercingsToShotsRatio));


        /// <summary>
        /// The damage required to achieve a <see cref="Wn8Rating"/> of Unicum.
        /// </summary>
        public double TargetDamageUnicum { get; set; }

        /// <summary>
        /// Piercing to achieve the Target Damage of Unicum WN8
        /// </summary>
        public int TargetDamageUnicumPiercings => (int)Math.Ceiling(TargetDamageUnicum / (LastMonth?.DamagePerEffectiveHit ?? DamagePerEffectiveHit));

        /// <summary>
        /// Shoots to achieve the Target Damage of Unicum WN8
        /// </summary>
        public int TargetDamageUnicumShots => (int)Math.Ceiling(TargetDamageUnicum / (LastMonth?.DamagePerEffectiveHit ?? DamagePerEffectiveHit) / (LastMonth?.PiercingsToShotsRatio ?? PiercingsToShotsRatio));

        /// <summary>
        /// The damage required to achieve a <see cref="Wn8Rating"/> of Super Unicum.
        /// </summary>
        public double TargetDamageSuperUnicum { get; set; }

        /// <summary>
        /// Piercing to achieve the Target Damage of Super Unicum WN8
        /// </summary>
        public int TargetDamageSuperUnicumPiercings => (int)Math.Ceiling(TargetDamageSuperUnicum / (LastMonth?.DamagePerEffectiveHit ?? DamagePerEffectiveHit));

        /// <summary>
        /// Shoots to achieve the Target Damage of Super Unicum WN8
        /// </summary>
        public int TargetDamageSuperUnicumShots => (int)Math.Ceiling(TargetDamageSuperUnicum / (LastMonth?.DamagePerEffectiveHit ?? DamagePerEffectiveHit) / (LastMonth?.PiercingsToShotsRatio ?? PiercingsToShotsRatio));


        #endregion

        /// <summary>
        /// Last Month Basic Statistics
        /// </summary>
        public TankReferenceBasic LastMonth { get; set; }

        /// <summary>
        /// Has a leader board?
        /// </summary>
        public bool HasLeaders => (Leaders != null) && (Leaders.Length > 0);

        

        /// <summary>
        /// Salva esse tanque em arquivo
        /// </summary>
        /// <returns>O full name do arquivo escrito</returns>
        /// <param name="path"></param>
        public string Save(string path)
        {
            var file = Path.Combine(path, $"Tank.{TankId:000000}.{Tag}.ref.json");
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(file, json, Encoding.UTF8);
            return file;
        }
    }
}