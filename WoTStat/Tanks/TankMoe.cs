using System;
using Newtonsoft.Json;

namespace Negri.Wot.Tanks
{
    /// <summary>
    ///     Um tanque, numa data, com seus valores de marca de excelência
    /// </summary>
    public class TankMoe : Tank
    {
        /// <summary>
        ///     Data do cálculo
        /// </summary>
        public DateTime Date { get; set; }        

        /// <summary>
        ///     O valor de 100% da marca
        /// </summary>
        public double HighMarkDamage { get; set; }

        /// <summary>
        ///     O valor 100% da marca, um dia atras
        /// </summary>
        [JsonIgnore]
        public double? HighMarkDamage1D { get; set; }

        /// <summary>
        ///     O valor 100% da marca, uma semana atras
        /// </summary>
        [JsonIgnore]
        public double? HighMarkDamage1W { get; set; }

        /// <summary>
        ///     O valor 100% da marca, um mês atras
        /// </summary>
        [JsonIgnore]
        public double? HighMarkDamage1M { get; set; }

        /// <summary>
        ///     Dano necessário para 1 marca
        /// </summary>
        public double Moe1Dmg => HighMarkDamage * 0.65;

        /// <summary>
        ///     Dano necessário para 2 marcas
        /// </summary>
        public double Moe2Dmg => HighMarkDamage * 0.85;

        /// <summary>
        ///     Dano necessário para 3 marcas
        /// </summary>
        public double Moe3Dmg => HighMarkDamage * 0.95;

        /// <summary>
        ///     Número de dias envolvidos no calculo. 14 é o ideal, mas podem ser menos em tanques pouco populares
        /// </summary>
        public int NumberOfDates { get; set; }

        /// <summary>
        ///     Número de Batalhas
        /// </summary>
        public long NumberOfBattles { get; set; }

        public double? Delta1D => HighMarkDamage1D.HasValue
            ? (HighMarkDamage - HighMarkDamage1D.Value) / HighMarkDamage1D.Value * 100.0
            : (double?) null;

        public double? Delta1W => HighMarkDamage1W.HasValue
            ? (HighMarkDamage - HighMarkDamage1W.Value) / HighMarkDamage1W.Value * 100.0
            : (double?) null;

        public double? Delta1M => HighMarkDamage1M.HasValue
            ? (HighMarkDamage - HighMarkDamage1M.Value) / HighMarkDamage1M.Value * 100.0
            : (double?) null;

        /// <summary>
        ///     Amostragem pequena, poucos dias
        /// </summary>
        public bool IsSuspectData => NumberOfBattles < 100 || NumberOfDates < 7;

        /// <summary>
        ///     Classe para amostragem pequena
        /// </summary>
        [JsonIgnore]
        public string SuspectDataClass => IsSuspectData ? "suspectData" : null;

        
    }
}