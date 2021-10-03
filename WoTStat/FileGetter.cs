using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using log4net;
using Negri.Wot.Tanks;
using Newtonsoft.Json;

namespace Negri.Wot
{
    /// <summary>
    ///     Lê informação em arquivos de dados
    /// </summary>
    public class FileGetter
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(FileGetter));

        private readonly string _dataDirectory;
        private readonly Guid _instanceId = Guid.NewGuid();
        private readonly object _clansLock = new();
        private readonly object _tanksLock = new();
                       
        /// <summary>
        ///     Cache dos dados mais recentes dos clãs
        /// </summary>
        private Dictionary<string, Clan> _clans;
        
        /// <summary>
        ///     Cache das Marcas de Excelência
        /// </summary>
        private Dictionary<DateTime, IDictionary<long, TankMoe>> _moes;

        /// <summary>
        ///     Cache dos líderes
        /// </summary>
        private Dictionary<DateTime, IEnumerable<Leader>> _leaders;

        /// <summary>
        ///     Cache dos WN8
        /// </summary>
        private Dictionary<DateTime, Wn8ExpectedValues> _wn8;

        /// <summary>
        ///     Cache dos tanques
        /// </summary>
        private Dictionary<long, TankReference> _tanks;

        public FileGetter(string dataDirectory)
        {
            _dataDirectory = dataDirectory;

            Log.DebugFormat("Instancia {0} criada no diretório {1}", _instanceId, _dataDirectory);
        }

        public IEnumerable<Clan> GetAllRecent(bool throwOnError = false)
        {
            lock (_clansLock)
            {
                if ((_clans != null) && (_clans.Count > 0))
                {
                    Log.Debug("Aproveitando clãs em cache.");
                    return _clans.Values;
                }

                var di = new DirectoryInfo(Path.Combine(_dataDirectory, "Clans"));
                var fis = di.EnumerateFiles("clan.*.json").Where(fi => fi.Length > 100);

                var allClans = fis
                    .Select(fi => Clan.FromFile(fi.FullName, throwOnError)).Where(c => !c.IsOnError).ToList();

                var duplicates = new HashSet<string>(allClans.GroupBy(c => c.ClanTag).Where(g => g.Count() > 1)
                    .Select(g => g.Key));

                _clans = allClans.Where(c => !duplicates.Contains(c.ClanTag)).ToDictionary(c => c.ClanTag);

                Log.DebugFormat("Colocados {0} clãs em cache.", _clans.Count);

                return _clans.Values;
            }
        }

        public Clan GetClan(string clanTag, bool throwOnError = true)
        {
            lock (_clansLock)
            {
                if (_clans != null && _clans.TryGetValue(clanTag, out var clan))
                {
                    Log.DebugFormat("Cache hit para {0}", clanTag);
                    return clan;
                }
                Log.DebugFormat("Cache miss para {0}", clanTag);
            }

            var fileName = Path.Combine(_dataDirectory, "Clans", $"clan.{clanTag}.json");
            return !File.Exists(fileName) ? null : Clan.FromFile(fileName);
        }

        /// <summary>
        ///     Verifica se o clã passado teve o nome mudado
        /// </summary>
        /// <param name="clanName"></param>
        /// <returns></returns>
        public string GetRenamedClan(string clanName)
        {
            var redirectFile = Path.Combine(_dataDirectory, "Renames");
            redirectFile = Path.Combine(redirectFile, clanName + ".ren.txt");
            if (!File.Exists(redirectFile))
            {
                return string.Empty;
            }

            var newName = File.ReadAllText(redirectFile, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(newName))
            {
                return string.Empty;
            }

            return newName;
        }

        public IDictionary<long, TankMoe> GetTanksMoe(DateTime? date = null)
        {
            lock (_tanksLock)
            {
                if (_moes == null)
                {
                    _moes = new Dictionary<DateTime, IDictionary<long, TankMoe>>();
                }

                var keyDate = date ?? DateTime.MinValue;
                if (_moes.TryGetValue(keyDate, out var tanks))
                {
                    Log.DebugFormat("Cache hit de MoE para {0:yyyy-MM-dd}", keyDate);
                    return tanks;
                }

                var dir = Path.Combine(_dataDirectory, "MoE");
                var di = new DirectoryInfo(dir);

                var dates = di.EnumerateFiles("????-??-??.moe.json")
                    .Where(fi => fi.Length > (10*1024))
                    .Select(fi => DateTime.ParseExact(fi.Name.Substring(0, 10), "yyyy-MM-dd",
                        CultureInfo.InvariantCulture))
                    .OrderByDescending(d => d).ToArray();

                if (date.HasValue)
                {
                    if (date.Value > dates[0])
                    {
                        date = dates[0];
                    }
                    else if (date.Value < dates.Last())
                    {
                        date = dates.Last();
                    }
                    else
                    {
                        date = dates.First(d => d <= date.Value);
                    }
                }
                else
                {
                    date = dates[0];
                }

                var fileName = Path.Combine(dir, $"{date.Value:yyyy-MM-dd}.moe.json");
                var json = File.ReadAllText(fileName, Encoding.UTF8);
                var moe = JsonConvert.DeserializeObject<Dictionary<long, TankMoe>>(json);

                var compareDate = date.Value.AddDays(-1);
                fileName = Path.Combine(dir, $"{compareDate:yyyy-MM-dd}.moe.json");
                if (File.Exists(fileName))
                {
                    json = File.ReadAllText(fileName, Encoding.UTF8);
                    var compare = JsonConvert.DeserializeObject<Dictionary<long, TankMoe>>(json);

                    foreach (var t in moe.Values)
                    {
                        if (compare.TryGetValue(t.TankId, out var tc))
                        {
                            t.HighMarkDamage1D = tc.HighMarkDamage;
                        }
                    }
                }

                compareDate = date.Value.AddDays(-7);
                fileName = Path.Combine(dir, $"{compareDate:yyyy-MM-dd}.moe.json");
                if (File.Exists(fileName))
                {
                    json = File.ReadAllText(fileName, Encoding.UTF8);
                    var compare = JsonConvert.DeserializeObject<Dictionary<long, TankMoe>>(json);

                    foreach (var t in moe.Values)
                    {
                        if (compare.TryGetValue(t.TankId, out var tc))
                        {
                            t.HighMarkDamage1W = tc.HighMarkDamage;
                        }
                    }
                }

                compareDate = date.Value.AddDays(-28);
                fileName = Path.Combine(dir, $"{compareDate:yyyy-MM-dd}.moe.json");
                if (File.Exists(fileName))
                {
                    json = File.ReadAllText(fileName, Encoding.UTF8);
                    var compare = JsonConvert.DeserializeObject<Dictionary<long, TankMoe>>(json);

                    foreach (var t in moe.Values)
                    {
                        if (compare.TryGetValue(t.TankId, out var tc))
                        {
                            t.HighMarkDamage1M = tc.HighMarkDamage;
                        }
                    }
                }

                _moes[date.Value] = moe;
                if (date.Value != keyDate)
                {
                    _moes[keyDate] = moe;
                }

                Log.DebugFormat("Cache miss de MoE para {0:yyyy-MM-dd} e {1:yyyy-MM-dd}", keyDate, date.Value);

                return moe;
            }
        }

        public IEnumerable<Leader> GetTankLeaders(DateTime? date = null)
        {
            lock (_tanksLock)
            {

                if (_leaders == null)
                {
                    _leaders = new Dictionary<DateTime, IEnumerable<Leader>>();
                }

                var keyDate = date ?? DateTime.MinValue;
                if (_leaders.TryGetValue(keyDate, out var leaders))
                {
                    Log.DebugFormat("Cache hit de Leaders para {0:yyyy-MM-dd}", keyDate);
                    return leaders;
                }

                var dir = Path.Combine(_dataDirectory, "Tanks");
                var di = new DirectoryInfo(dir);

                // Unlikely the leaders file became less than 9MB
                const int minFileSize = 9;

                var dates = di.EnumerateFiles("????-??-??.Leaders.json")
                    .Where(fi => fi.Length > minFileSize * 1024*1024)
                    .Select(fi => DateTime.ParseExact(fi.Name.Substring(0, 10), "yyyy-MM-dd",
                        CultureInfo.InvariantCulture))
                    .OrderByDescending(d => d).ToArray();

                if (dates.Length == 0)
                {
                    return Enumerable.Empty<Leader>();
                }

                if (date.HasValue)
                {
                    if (date.Value > dates[0])
                    {
                        date = dates[0];
                    }
                    else if (date.Value < dates.Last())
                    {
                        date = dates.Last();
                    }
                    else
                    {
                        date = dates.First(d => d <= date.Value);
                    }
                }
                else
                {
                    date = dates[0];
                }

                var fileName = Path.Combine(dir, $"{date.Value:yyyy-MM-dd}.Leaders.json");
                var json = File.ReadAllText(fileName, Encoding.UTF8);
                try
                {
                    leaders = JsonConvert.DeserializeObject<List<Leader>>(json);
                }
                catch (JsonException ex)
                {
                    if (ex is JsonSerializationException sex)
                    {
                        Log.Error($"Error parsing Leaders file at {fileName}, Line {sex.LineNumber} at position {sex.LinePosition}", sex);
                    }
                    else
                    {
                        Log.Error($"Error parsing Leaders file at {fileName} with {json.Length} chars", ex);
                    }

                    if (dates.Length > 1)
                    {
                        // Use the next one, on the hope it's works
                        date = dates[1];
                        return GetTankLeaders(date);
                    }

                    return Enumerable.Empty<Leader>();
                }
                
                _leaders[date.Value] = leaders;
                if (date.Value != keyDate)
                {
                    _leaders[keyDate] = leaders;
                }

                Log.DebugFormat("Cache miss de Leaders para {0:yyyy-MM-dd} e {1:yyyy-MM-dd}", keyDate, date.Value);

                return leaders;
            }            
        }

        public Wn8ExpectedValues GetTanksWN8ReferenceValues(DateTime? date = null)
        {
            lock (_tanksLock)
            {

                if (_wn8 == null)
                {
                    _wn8 = new Dictionary<DateTime, Wn8ExpectedValues>();
                }

                var keyDate = date ?? DateTime.MinValue;
                if (_wn8.TryGetValue(keyDate, out var wn8))
                {
                    Log.DebugFormat("Cache hit de WN8 para {0:yyyy-MM-dd}", keyDate);
                    return wn8;
                }

                var dir = Path.Combine(_dataDirectory, "MoE");
                var di = new DirectoryInfo(dir);

                var dates = di.EnumerateFiles("????-??-??.WN8.json")
                    .Where(fi => fi.Length >= 100 * 1024)
                    .Select(fi => DateTime.ParseExact(fi.Name.Substring(0, 10), "yyyy-MM-dd",
                        CultureInfo.InvariantCulture))
                    .OrderByDescending(d => d).ToArray();

                if (dates.Length == 0)
                {
                    return null;
                }

                if (date.HasValue)
                {
                    if (date.Value > dates[0])
                    {
                        date = dates[0];
                    }
                    else if (date.Value < dates.Last())
                    {
                        date = dates.Last();
                    }
                    else
                    {
                        date = dates.First(d => d <= date.Value);
                    }
                }
                else
                {
                    date = dates[0];
                }

                var fileName = Path.Combine(dir, $"{date.Value:yyyy-MM-dd}.WN8.json");
                var json = File.ReadAllText(fileName, Encoding.UTF8);
                wn8 = JsonConvert.DeserializeObject<Wn8ExpectedValues>(json);

                _wn8[date.Value] = wn8;
                if (date.Value != keyDate)
                {
                    _wn8[keyDate] = wn8;
                }

                Log.DebugFormat("Cache miss de WN8 para {0:yyyy-MM-dd} e {1:yyyy-MM-dd}", keyDate, date.Value);

                return wn8;
            }
        }

        public TankReference GetTankReference(long tankId)
        {
            lock (_tanksLock)
            {
                if (_tanks == null)
                {
                    _tanks = new Dictionary<long, TankReference>();
                }
            }

            if (_tanks.TryGetValue(tankId, out var tr))
            {
                return tr;
            }

            var dir = Path.Combine(_dataDirectory, "Tanks");
            var di = new DirectoryInfo(dir);

            var fi = di.EnumerateFiles($"Tank.{tankId:D6}.*.ref.json").FirstOrDefault();
            if (fi == null)
            {
                _tanks[tankId] = null;
                return null;
            }

            var json = File.ReadAllText(fi.FullName, Encoding.UTF8);
            tr = JsonConvert.DeserializeObject<TankReference>(json);

            _tanks[tankId] = tr;
            return tr;
        }
    }
}