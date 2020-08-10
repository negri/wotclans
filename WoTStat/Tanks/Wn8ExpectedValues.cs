using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using log4net;
using Newtonsoft.Json;
using OfficeOpenXml;

namespace Negri.Wot.Tanks
{
    /// <summary>
    /// Os valores esperados para os tanques
    /// </summary>
    public class Wn8ExpectedValues
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Wn8ExpectedValues));

        private Dictionary<long, Wn8TankExpectedValues> _values = new Dictionary<long, Wn8TankExpectedValues>();

        /// <summary>
        /// Dados dos dados
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Origem (XVM, WoTClans etc)
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Versão dos dados (uma data, um numero de versão etc)
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Adiciona uma referência
        /// </summary>
        /// <param name="ev"></param>
        public void Add(Wn8TankExpectedValues ev)
        {
            _values[ev.TankId] = ev;
        }

        /// <summary>
        /// All tanks
        /// </summary>
        public Wn8TankExpectedValues[] AllTanks
        {
            get { return _values.Values.OrderBy(ev => ev.TankId).ToArray(); }
            set { _values = value.ToDictionary(t => t.TankId); }
        }

        public int Count => _values.Count;

        public double CalculateWn8(TankPlayerStatistics tank)
        {
            return CalculateWn8(new[] {tank});
        }

        
        /// <summary>
        /// Devolve os esperados de um tanque a partir do ID
        /// </summary>
        public Wn8TankExpectedValues this[long tankId] => _values.TryGetValue(tankId, out var ev) ? ev : null;

        /// <summary>
        /// Calculate the WN8 of a set of tanks
        /// </summary>
        /// <param name="played"></param>
        /// <returns></returns>
        public double CalculateWn8(IEnumerable<TankPlayerStatistics> played)
        {
            return CalculateWn8(played.ToDictionary(t => t.TankId));
        }

        /// <summary>
        /// Calcula o WN8 de um conjunto de tanques jogados
        /// </summary>
        /// <param name="played">Dicionário com o ID de cada tanque jogado e seus valores totais</param>
        /// <returns>o WN8</returns>
        public double CalculateWn8(IDictionary<long, TankPlayerStatistics> played)
        {
            if (played == null)
            {
                return 0;
            }

            if (played.Count == 0)
            {
                return 0;
            }

            if ((_values == null) || (_values.Count == 0))
            {
                throw new InvalidOperationException("There are no expected values!");
            }

            double sumPlayedDamage = 0, sumRefDamage = 0;
            double sumPlayedWin = 0, sumRefWin = 0;
            double sumPlayedFrag = 0, sumRefFrag = 0;
            double sumPlayedSpot = 0, sumRefSpot = 0;
            double sumPlayedDef = 0, sumRefDef = 0;
                                    
            foreach (var p in played)
            {
                if (_values.TryGetValue(p.Key, out var exp))
                {
                    sumPlayedDamage += p.Value.DamageDealt;
                    sumPlayedWin += p.Value.Wins;
                    sumPlayedFrag += p.Value.Kills;
                    sumPlayedSpot += p.Value.Spotted;
                    sumPlayedDef += p.Value.DroppedCapturePoints;

                    sumRefDamage +=  (exp.Damage * p.Value.Battles);
                    sumRefWin += (exp.WinRate * p.Value.Battles);
                    sumRefFrag += (exp.Frag * p.Value.Battles);
                    sumRefSpot += (exp.Spot * p.Value.Battles);
                    sumRefDef += (exp.Def * p.Value.Battles);

                    // The WN8 for only this tank
                    var relTankDamage = p.Value.DamageDealt / (exp.Damage * p.Value.Battles);
                    var relTankWin = p.Value.Wins / (exp.WinRate * p.Value.Battles);
                    var relTankFrag = p.Value.Kills / (exp.Frag * p.Value.Battles);
                    var relTankSpot = p.Value.Spotted / (exp.Spot * p.Value.Battles);
                    var relTankDef = p.Value.DroppedCapturePoints / (exp.Def * p.Value.Battles);
                    p.Value.Wn8 = Wn8TankExpectedValues.GetWn8FromRelatives(relTankDamage, relTankWin, relTankFrag, relTankSpot, relTankDef);
                }
                else
                {
                    Log.Debug($"Played Tank id {p.Key} could not be found on the Expected Values.");
                }
            }

            var relDamage = sumPlayedDamage / sumRefDamage;
            var relWin = sumPlayedWin / sumRefWin;
            var relFrag = sumPlayedFrag / sumRefFrag;
            var relSpot = sumPlayedSpot / sumRefSpot;
            var relDef = sumPlayedDef / sumRefDef;

            return Wn8TankExpectedValues.GetWn8FromRelatives(relDamage, relWin, relFrag, relSpot, relDef);
        }

        /// <summary>
        /// Devolve os bytes de uma planilha excel com o conteúdo
        /// </summary>
        /// <returns></returns>
        public byte[] GetExcel()
        {
            var codeBase = Assembly.GetExecutingAssembly().CodeBase;
            var uri = new UriBuilder(codeBase);
            var path = Uri.UnescapeDataString(uri.Path);
            var dir = Path.GetDirectoryName(path);
            Debug.Assert(dir != null);
            var template = Path.Combine(dir, "WN8Reference.Template.xlsx");
            if (!File.Exists(template))
            {
                throw new FileNotFoundException("Coult not find the template file!", template);
            }

            using (var p = new ExcelPackage(new FileInfo(template)))
            {
                var ws = p.Workbook.Worksheets["WN8 Reference"];

                ws.Cells[3, 2].Value = Source;
                ws.Cells[4, 2].Value = Version;
                ws.Cells[5, 2].Value = Date;

                int r = 7;

                ws.Cells[r, 1, r, 12].AutoFilter = true;

                foreach (var t in _values.Values.OrderByDescending(t => t.Tier).ThenByDescending(t => t.TypeName).ThenBy(t => t.NatioName).ThenBy(t => t.Name))
                {
                    ++r;

                    ws.Cells[r, 1].Value = t.TankId;
                    ws.Cells[r, 2].Value = t.Name;
                    ws.Cells[r, 3].Value = t.Tier;
                    ws.Cells[r, 4].Value = t.TypeName;
                    ws.Cells[r, 5].Value = t.NatioName;
                    ws.Cells[r, 6].Value = t.IsPremium;
                    ws.Cells[r, 7].Value = t.Damage;
                    ws.Cells[r, 8].Value = t.WinRate;
                    ws.Cells[r, 9].Value = t.Frag;
                    ws.Cells[r, 10].Value = t.Spot;
                    ws.Cells[r, 11].Value = t.Def;
                    ws.Cells[r, 12].Value = t.Origin.ToString();
                }

                return p.GetAsByteArray();
            }
        }

        /// <summary>
        /// Calcula o Tier Medio de um conjunto de batalhas jogadas
        /// </summary>
        /// <param name="played"></param>
        /// <returns></returns>
        public decimal CalculateTier(IDictionary<long, TankPlayerStatistics> played)
        {
            decimal sumBattles = 0;
            decimal sumBattlesTier = 0;

            foreach (var p in played)
            {
                if (_values.TryGetValue(p.Key, out var exp))
                {
                    sumBattles += p.Value.Battles;
                    sumBattlesTier += p.Value.Battles * exp.Tier;
                }
                else
                {
                    Log.Debug($"Played Tank id {p.Key} could not be found on the Expected Values.");
                }
            }

            var tier = sumBattlesTier / sumBattles;
            return tier;
        }

        /// <summary>
        /// If the Tank is know
        /// </summary>
        public bool Contains(long tankId)
        {
            return _values.ContainsKey(tankId);
        }

        public void Remove(long tankId)
        {
            _values.Remove(tankId);
        }

        public void ToFile(string rootDataDirectory)
        {
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            var file = Path.Combine(rootDataDirectory, "MoE", $"{Date:yyyy-MM-dd}.WN8.json");
            File.WriteAllText(file, json, Encoding.UTF8);
        }
    }
}