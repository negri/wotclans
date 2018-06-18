using System;
using System.Text;
using System.Threading.Tasks;
using log4net;
using Negri.Wot.Mathematic;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Math;

namespace Negri.Wot.Tanks
{
    /// <summary>
    /// Os valores experados para o calculo de WN8, em um tanque
    /// </summary>
    public class Wn8TankExpectedValues : Tank
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Wn8TankExpectedValues));

        /// <summary>
        /// Esperado em Damage
        /// </summary>
        public double Damage { get; set; }

        /// <summary>
        /// Esperado em WinRate
        /// </summary>
        public double WinRate { get; set; }

        /// <summary>
        /// Esperado em Kills
        /// </summary>
        public double Frag { get; set; }

        /// <summary>
        /// Esperado em Spot
        /// </summary>
        public double Spot { get; set; }

        /// <summary>
        /// Esperado em defesa
        /// </summary>
        public double Def { get; set; }                       

        /// <summary>
        /// Origem dos dados
        /// </summary>
        public Wn8TankExpectedValuesOrigin Origin { get; set; } = Wn8TankExpectedValuesOrigin.Source;

        /// <summary>
        /// Calcula o WN8 em relação a esses valores
        /// </summary>
        /// <param name="damage">Dano direto por partida</param>
        /// <param name="win">Taxa de Vitorias</param>
        /// <param name="frag">Kills por partida</param>
        /// <param name="spot">Espotados por partida</param>
        /// <param name="def">Pontos de Defesa por partida</param>
        /// <returns>O WN8</returns>
        public double GetWn8(double damage, double win, double frag, double spot, double def)
        {
            var relDamage = (damage / Damage);
            var relWin = (win / WinRate);
            var relFrag = (frag / Frag);
            var relSpot = (spot / Spot);
            var relDef = (def / Def);

            return GetWn8FromRelatives(relDamage, relWin, relFrag, relSpot, relDef);
        }

        /// <summary>
        /// Returns the Target Damage to achieve a given WN8 Rating
        /// </summary>
        /// <param name="rating">The target Rating</param>
        /// <returns>The target Damage</returns>
        public double GetTargetDamage(Wn8Rating rating)
        {            
            double damage = GetTargetDamage((int)rating);

            // round on 10...
            damage = Math.Round(damage / 10.0) * 10.0;
            return damage;
        }

        /// <summary>
        /// Returns the Target Damage to achieve a given WN8 value
        /// </summary>
        public double GetTargetDamage(double wn8)
        {
            var solution = EquationSolver.Solve(EquationSolver.Method.Brent, 0.0, 10000.0, damage => wn8 - GetWn8(damage),
                new EquationSolver.StopConditions(null, null, 1.0, TimeSpan.FromMilliseconds(100)));
            if (solution.Success)
            {
                return solution.Result;
            }
           
            Log.Warn($"Could not find the Target Damage for WN8 of {wn8:N0} on tank {TankId}.{Name}: {solution.StopReasons}");

            return 0.0;
        }

        /// <summary>
        /// The WN8 if every other variable, except damage, is the same as the expected values
        /// </summary>
        /// <param name="damage"></param>
        /// <returns></returns>
        public double GetWn8(double damage)
        {
            return GetWn8FromRelatives(damage / Damage, 1.0, 1.0, 1.0, 1.0);
        }

        public static double GetWn8FromRelatives(double relDamage, double relWin, double relFrag, double relSpot, double relDef)
        {
            var normDamage = Math.Max(0, (relDamage - 0.22) / (1.0 - 0.22));
            var normWin = Math.Max(0, (relWin - 0.71) / (1.0 - 0.71));
            var normFrag = Math.Max(0, Math.Min(normDamage + 0.2, (relFrag - 0.12) / (1 - 0.12)));
            var normSpot = Math.Max(0, Math.Min(normDamage + 0.1, (relSpot - 0.38) / (1 - 0.38)));
            var normDef = Math.Max(0, Math.Min(normDamage + 0.1, (relDef - 0.10) / (1 - 0.10)));

            var damagePart = 980.0 * normDamage;
            var winPart = 145.0 * Math.Min(1.8, normWin);
            var fragPart = 210.0 * normDamage * normFrag;
            var spotPart = 155.0 * normFrag * normSpot;
            var defPart = 75.0 * normFrag * normDef;

            return damagePart + winPart + fragPart + spotPart + defPart;
        }

    }
}
