using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Negri.Wot.Tanks
{
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
        /// <param name="rootDataDirectory"></param>
        public string ToFile(string rootDataDirectory)
        {
            var file = Path.Combine(rootDataDirectory, "Tanks", $"Tank.{TankId:000000}.{Tag}.ref.json");
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(file, json, Encoding.UTF8);
            return file;
        }
    }
}