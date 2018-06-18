using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using Negri.Wot.Diagnostics;
using Negri.Wot.Tanks;
using Negri.Wot.WgApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Tank = Negri.Wot.WgApi.Tank;

namespace Negri.Wot
{
    /// <summary>
    ///     Lê informações de um clã da Web
    /// </summary>
    public class Fetcher
    {
        private const int MaxTry = 10;

        private static readonly ILog Log = LogManager.GetLogger(typeof(Fetcher));

        private static readonly Regex RegexWotInfoPlayerStat =
            new Regex("<div class=\"col-xs-4 col-sm-4.*?>\\s+([\\d,\\.]*)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex RegexWotInfoPlayerTotal = new Regex("<strong>\\s*([\\d,\\.]*)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly string _cacheDirectory;

        private DateTime _lastWebFetch = DateTime.MinValue;

        public Fetcher(string cacheDirectory = null)
        {
            _cacheDirectory = cacheDirectory ?? Path.GetTempPath();

            WebFetchInterval = TimeSpan.FromSeconds(30);
            WebCacheAge = TimeSpan.FromDays(1) - TimeSpan.FromHours(1);

            ServicePointManager.ServerCertificateValidationCallback =
                ServerCertificateValidationCallback;
        }

        /// <summary>
        ///     Id da aplicação para consultas na API da Wargaming
        /// </summary>
        public string ApplicationId { set; private get; } = "demo";

        public TimeSpan WebCacheAge { set; private get; }

        public TimeSpan WebFetchInterval { set; private get; }

        private static bool ServerCertificateValidationCallback(object s, X509Certificate certificate, X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return true;
            }

            string expDateString = certificate.GetExpirationDateString();
            DateTime expDate = DateTime.Parse(expDateString, CultureInfo.CurrentCulture);
            if (expDate < DateTime.Now)
            {
                // Assumindo que vencido não é um problema sério
                return true;
            }

            return false;
        }

        public SiteDiagnostic GetSiteDiagnostic(Plataform plataform, string apiKey)
        {
            var plataformPrefix = plataform == Plataform.PS ? "ps." : string.Empty;
            string url = $"https://{plataformPrefix}wotclans.com.br/api/status";
            return GetSiteDiagnostic(url, apiKey);
        }

        public SiteDiagnostic GetSiteDiagnostic(string apiUrl, string apiKey)
        {
            Log.Debug("Obtendo o diagnostico do site remoto");
            var content = GetContent($"SiteDiagnostic.{DateTime.UtcNow:yyyy-MM-dd.HHmmss}.json", apiUrl, WebCacheAge,
                false, Encoding.UTF8).Result;
            var json = content.Content;
            var siteDiagnostic = JsonConvert.DeserializeObject<SiteDiagnostic>(json);
            return siteDiagnostic;
        }

        /// <summary>
        ///     Obtem um tanque especifico
        /// </summary>
        public Tank GetTank(Plataform plataform, long tankId)
        {
            return GetTanks(plataform, tankId).FirstOrDefault();
        }

        /// <summary>
        ///     Obtem todos os tanques do jogo
        /// </summary>
        public IEnumerable<Tank> GetTanks(Plataform plataform)
        {
            return GetTanks(plataform, null);
        }

        public async Task<Wn8ExpectedValues> GetXvmWn8ExpectedValuesAsync()
        {
            Log.Debug("Obtendo os WN8 da XVM");
            const string url = "https://static.modxvm.com/wn8-data-exp/json/wn8exp.json";
            var content = await GetContent("Wn8XVM.json", url, WebCacheAge, false, Encoding.UTF8);
            var json = content.Content;

            var ev = new Wn8ExpectedValues();

            var j = JObject.Parse(json);
            var h = j["header"];

            ev.Source = (string)h["source"];
            ev.Version = (string)h["version"];

            var d = j["data"];
            foreach (var dd in d.Children())
            {
                ev.Add(new Wn8TankExpectedValues
                {
                    TankId = (long)dd["IDNum"],
                    Def = (double)dd["expDef"],
                    Frag = (double)dd["expFrag"],
                    Spot = (double)dd["expSpot"],
                    Damage = (double)dd["expDamage"],
                    WinRate = (double)dd["expWinRate"] / 100.0
                });
            }

            return ev;
        }

        /// <summary>
        ///     Obtem o WN8 do jogador na api do WoTStatConsole.de
        /// </summary>
        public async Task<Player> GetPlayerWn8Async(Player player)
        {
            Log.DebugFormat("Obtendo WN8 do jogador {0}@{1}...", player.Id, player.Plataform);

            string url =
                $"https://wotstatsconsole.de/api/rest?request=playerdata&id={player.Id}&sys={(player.Plataform == Plataform.PS ? "ps4" : "xbox")}";
            var content =
                await
                    GetContent($"WotStatsConsole.{player.Plataform}.{player.Id}.json", url, WebCacheAge, false,
                        Encoding.UTF8);
            var json = content.Content;

            try
            {
                var j = JObject.Parse(json);
                var d = j["data"];
                var overall = d["statistics"];
                var month = d["month"];

                try
                {
                    player.TotalBattles = (int)overall["allBattles"];
                    player.TotalWinRate = (double)overall["winRate"];
                    player.TotalWn8 = (double)overall["wn8"];
                }
                catch
                {
                    // Indica que o cidadão nunca jogou
                    Log.Debug($"Player {player.Plataform}.{player.Id}.{player.Name} never played a single game!");
                    return null;
                }

                // Se é a primeira vez do cidadão nesse site os mensais voltam zerados
                try
                {
                    player.MonthBattles = (int)month["allBattles"];
                    player.MonthWinRate = (double)month["allwins"] / (double)month["allBattles"];
                    player.MonthWn8 = (double)month["wn8"];
                }
                catch
                {
                    Log.Debug(
                        $"Mensais de {player.Plataform}.{player.Id}.{player.Name} estão zeradas. 1ª vez no site. Extrapolando.");

                    DateTime playsFrom = ((long)d["overall"]["created_at"]).ToDateTime();
                    var daysInGame = (DateTime.UtcNow - playsFrom).TotalDays;
                    var monthsInGame = daysInGame / 28.0;

                    player.MonthBattles = (int)(player.TotalBattles / monthsInGame);
                    player.MonthWinRate = player.TotalWinRate;
                    player.MonthWn8 = player.TotalWn8;
                    player.IsPatched = true;
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"Erro no JSON de {player.Plataform}.{player.Id}.{player.Name}", ex);
                return null;
            }

            player.Moment = DateTime.UtcNow;
            player.Origin = PlayerDataOrigin.WotStatConsole;

            Log.Debug(
                $"Retornando {player.Plataform}.{player.Id}.{player.Name}: {player.MonthBattles}; {player.MonthWn8}");

            return player;
        }

        /// <summary>
        ///     Obtem os tanques de um jogador
        /// </summary>
        public IEnumerable<TankPlayer> GetTanksForPlayer(Plataform plataform, long playerId, long? tankId = null)
        {
            Log.DebugFormat("Obtendo tanques do jogador {0}@{1}...", playerId, plataform);

            string server;
            switch (plataform)
            {
                case Plataform.XBOX:
                    server = "api-xbox-console.worldoftanks.com";
                    break;
                case Plataform.PS:
                    server = "api-ps4-console.worldoftanks.com";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(plataform), plataform, null);
            }

            string requestUrl =
                $"https://{server}/wotx/tanks/stats/?application_id={ApplicationId}&account_id={playerId}";
            if (tankId.HasValue)
            {
                requestUrl += $"&tank_id={tankId.Value}";
            }

            var content =
                    GetContent($"TanksStats.{plataform}.{playerId}.json", requestUrl, WebCacheAge, false,
                        Encoding.UTF8).Result;
            var json = content.Content;
            var response = JsonConvert.DeserializeObject<TanksStatsResponse>(json);
            if (response.IsError)
            {
                Log.Error(response.Error);
                return Enumerable.Empty<TankPlayer>();
            }

            var list = new List<TankPlayer>();
            foreach (var tankPlayers in response.Tanks.Values)
            {
                if (tankPlayers != null)
                {
                    foreach (var tankPlayer in tankPlayers)
                    {
                        tankPlayer.Plataform = plataform;
                        list.Add(tankPlayer);
                    }
                }
            }

            return list;
        }

        /// <summary>
        ///     Obtem os tanques de um jogador
        /// </summary>
        public async Task<IEnumerable<TankPlayer>> GetTanksForPlayerAsync(Plataform plataform, long playerId,
            long? tankId = null)
        {
            Log.DebugFormat("Obtendo tanques do jogador {0}@{1}...", playerId, plataform);

            string server;
            switch (plataform)
            {
                case Plataform.XBOX:
                    server = "api-xbox-console.worldoftanks.com";
                    break;
                case Plataform.PS:
                    server = "api-ps4-console.worldoftanks.com";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(plataform), plataform, null);
            }

            string requestUrl =
                $"https://{server}/wotx/tanks/stats/?application_id={ApplicationId}&account_id={playerId}";
            if (tankId.HasValue)
            {
                requestUrl += $"&tank_id={tankId.Value}";
            }

            var content =
                await
                    GetContent($"TanksStats.{plataform}.{playerId}.json", requestUrl, WebCacheAge, false,
                        Encoding.UTF8);
            var json = content.Content;
            var response = JsonConvert.DeserializeObject<TanksStatsResponse>(json);
            if (response.IsError)
            {
                Log.Error(response.Error);
                return Enumerable.Empty<TankPlayer>();
            }

            var list = new List<TankPlayer>();
            foreach (var tankPlayers in response.Tanks.Values)
            {
                if (tankPlayers != null)
                {
                    foreach (var tankPlayer in tankPlayers)
                    {
                        tankPlayer.Plataform = plataform;
                        list.Add(tankPlayer);
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Because the PC API has pagination
        /// </summary>
        private IEnumerable<Tank> GetPcTanks(long? tankId)
        {
            var tanks = new List<Tank>();

            int page = 1, totalPages = 0;
            do
            {
                var url =
                    $"https://api.worldoftanks.com/wot/encyclopedia/vehicles/?application_id={ApplicationId}&fields=tank_id%2Ctier%2Ctype%2Cshort_name%2Ctag%2Cis_premium%2Cnation%2Cname&page_no={page}&limit=100";
                if (tankId.HasValue)
                {
                    url += $"&tank_id={tankId.Value}";
                }

                var json = GetContentSync($"Vehicles.{Plataform.PC}.{DateTime.UtcNow:yyyyMMddHH}.{page}.json", url, WebCacheAge, false, Encoding.UTF8).Content;
                var response = JsonConvert.DeserializeObject<VehiclesResponse>(json);
                if (response.IsError)
                {
                    Log.Error(response.Error);
                    return Enumerable.Empty<Tank>();
                }

                tanks.AddRange(response.Data.Values);

                totalPages = response.Meta.PageTotal;
                page = page + 1;
            } while (page <= totalPages);

            foreach (var tank in tanks)
            {
                tank.Plataform = Plataform.PC;
            }

            return tanks;
        }

        private IEnumerable<Tank> GetTanks(Plataform plataform, long? tankId)
        {
            Log.DebugFormat("Procurando dados de tanques para {0}...", plataform);
            string server;
            switch (plataform)
            {
                case Plataform.XBOX:
                    server = "api-xbox-console.worldoftanks.com";
                    break;
                case Plataform.PS:
                    server = "api-ps4-console.worldoftanks.com";
                    break;
                case Plataform.PC:
                    return GetPcTanks(tankId);
                default:
                    throw new ArgumentOutOfRangeException(nameof(plataform), plataform, null);
            }

            string requestUrl =
                $"https://{server}/wotx/encyclopedia/vehicles/?application_id={ApplicationId}&fields=tank_id%2Cname%2Cshort_name%2Cis_premium%2Ctier%2Ctag%2Ctype%2Cimages%2Cnation";
            if (tankId != null)
            {
                requestUrl += $"&tank_id={tankId.Value}";
            }

            var json =
                GetContentSync($"Vehicles.{plataform}.{DateTime.UtcNow:yyyyMMddHH}.json", requestUrl, WebCacheAge,
                    false,
                    Encoding.UTF8).Content;

            var response = JsonConvert.DeserializeObject<VehiclesResponse>(json);
            if (response.IsError)
            {
                Log.Error(response.Error);
                return Enumerable.Empty<Tank>();
            }

            foreach (var tank in response.Data.Values)
            {
                tank.Plataform = plataform;
            }

            return response.Data.Values;
        }


        /// <summary>
        ///     Encontra um clã a partir da tag dele
        /// </summary>
        public Clan FindClan(Plataform plataform, string clanTag, bool returnSmallClans = false)
        {
            Log.DebugFormat("Procurando dados do clã {0}@{1}...", clanTag, plataform);
            string server;
            switch (plataform)
            {
                case Plataform.XBOX:
                    server = "api-xbox-console.worldoftanks.com";
                    break;
                case Plataform.PS:
                    server = "api-ps4-console.worldoftanks.com";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(plataform), plataform, null);
            }

            string requestUrl =
                $"https://{server}/wotx/clans/list/?application_id={ApplicationId}&search={clanTag}&limit=1";

            var json =
                GetContentSync($"FindClan.{plataform}.{clanTag}.json", requestUrl, TimeSpan.FromMinutes(1), false,
                    Encoding.UTF8).Content;

            var response = JsonConvert.DeserializeObject<ClansListResponse>(json);
            if (response.IsError)
            {
                Log.Error(response.Error);
                return null;
            }

            // Deve coincidir
            if (response.Clans.Length != 1)
            {
                Log.Error($"Há {response.Clans.Length} respostas na busca por {clanTag}.");
                return null;
            }

            var found = response.Clans[0];
            if (string.Compare(clanTag, found.Tag, StringComparison.OrdinalIgnoreCase) != 0)
            {
                Log.Error($"A busca por '{clanTag}' encontrou apenas '{found.Tag}', que não coincide");
                return null;
            }

            if ((found.MembersCount < 7) && (!returnSmallClans))
            {
                Log.Error(
                    $"A busca por '{clanTag}' encontrou apenas {found.MembersCount} membros no clã id {found.ClanId}. Ele não será adicionado.");
                return null;
            }

            return new Clan(plataform, found.ClanId, found.Tag) { AllMembersCount = found.MembersCount };
        }

        /// <summary>
        ///     Obtem o jogado em WoTInfo
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public Player GetPlayer(Player player)
        {
            Log.DebugFormat("Obtendo jogador {0}.{1}, @{2}, do clan '{3}'...", player.Name, player.Id, player.Plataform,
                player.ClanTag ?? string.Empty);

            var statUrl = $"http://wotinfo.net/en/recent?playerid={player.Id}&server=";
            switch (player.Plataform)
            {
                case Plataform.XBOX:
                    statUrl += "xbox";
                    break;
                case Plataform.PS:
                    statUrl += "ps4";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(player.Plataform));
            }

            var statFile = $"WoTInfo.{player.Id}.txt";

            var moment = DateTime.UtcNow;
            var content = GetContentSync(statFile, statUrl, WebCacheAge, false, Encoding.UTF8).Content;

            for (var i = 0; i < MaxTry; ++i)
            {
                if (ParsePlayerStatsPage(player, content, out var parsePlayerError))
                {
                    break;
                }

                Log.WarnFormat("Erro de parsing na página do jogador {0}.{1}@{2}. Motivo: {3}. Arquivo: {4}", player.Id,
                    player.Name, player.Plataform, parsePlayerError, statFile);
                if (parsePlayerError == ParsePlayerError.WgApiIsDown)
                {
                    // Não adianta tentar novamente agora
                    DeleteFromCache(statFile);
                    return player;
                }

                var secondsWait = 5 + 2 * i;
                Log.WarnFormat(
                    "...sem dados na página do WoTInfo para o jogador {0}@{2}. Esperando {1}s antes de tentar novamente.",
                    player.Name, secondsWait, player.Plataform);
                Thread.Sleep(TimeSpan.FromSeconds(secondsWait));

                DeleteFromCache(statFile);

                var fullContent = GetContentSync(statFile, statUrl, WebCacheAge, true, Encoding.UTF8);
                content = fullContent.Content;
                moment = fullContent.Moment;
            }

            player.Moment = moment;
            player.Origin = PlayerDataOrigin.WotInfo;

            Log.DebugFormat("  Batalhas: {0:N0}; WN8a: {1:r}", player.MonthBattles, player.MonthWn8);

            return player;
        }

        private WebContent GetContentSync(string cacheFileTitle, string url, TimeSpan maxCacheAge, bool noWait,
            Encoding encoding = null)
        {
            var task = GetContent(cacheFileTitle, url, maxCacheAge, noWait, encoding);
            task.Wait();
            return task.Result;
        }

        private async Task<WebContent> GetContent(string cacheFileTitle, string url, TimeSpan maxCacheAge, bool noWait,
            Encoding encoding = null)
        {
            Log.DebugFormat("Obtendo '{0}' ...", url);

            encoding = encoding ?? Encoding.UTF8;

            var cacheFileName = Path.Combine(_cacheDirectory, cacheFileTitle);
            if (!File.Exists(cacheFileName))
            {
                Log.Debug("...nunca pego antes...");
                return await GetContentFromWeb(cacheFileName, url, noWait, encoding);
            }

            var fi = new FileInfo(cacheFileName);
            var moment = fi.LastWriteTimeUtc;
            var age = DateTime.UtcNow - moment;
            if (age > maxCacheAge)
            {
                Log.DebugFormat("...arquivo de cache em '{0}' de {1:yyyy-MM-dd HH:mm} expirado com {2:N0}h...",
                    cacheFileTitle, moment, age.TotalHours);

                return await GetContentFromWeb(cacheFileName, url, noWait, encoding);
            }

            Log.Debug("...Obtido do cache.");
            return new WebContent(File.ReadAllText(cacheFileName, encoding)) { Moment = moment };
        }

        private async Task<WebContent> GetContentFromWeb(string cacheFileName, string url, bool noWait,
            Encoding encoding)
        {
            var timeSinceLastFetch = DateTime.UtcNow - _lastWebFetch;
            var waitTime = WebFetchInterval - timeSinceLastFetch;
            var waitTimeMs = Math.Max((int)waitTime.TotalMilliseconds, 0);
            if (!noWait & (waitTimeMs > 0))
            {
                Log.DebugFormat("...esperando {0:N1}s para usar a web...", waitTimeMs / 1000.0);
                Thread.Sleep(waitTimeMs);
            }

            Exception lastException = new ApplicationException("Erro no controle de fluxo!");

            for (var i = 0; i < MaxTry; ++i)
            {
                try
                {
                    var moment = DateTime.UtcNow;
                    var sw = Stopwatch.StartNew();

                    var webClient = new WebClient();
                    webClient.Headers.Add("user-agent",
                        "GetClanStats (WoTClansBrCollector) by JP Negri at negrijp _at_ gmail.com");
                    var bytes = await webClient.DownloadDataTaskAsync(url);
                    var webTime = sw.ElapsedMilliseconds;

                    var content = Encoding.UTF8.GetString(bytes);

                    // Escreve em cache
                    sw.Restart();

                    for (int j = 0; j < MaxTry; ++j)
                    {
                        try
                        {
                            File.WriteAllText(cacheFileName, content, encoding);
                            break;
                        }
                        catch (IOException ex)
                        {
                            if (j < MaxTry - 1)
                            {
                                Log.Warn("...esperando antes de tentar novamente.");
                                Thread.Sleep(TimeSpan.FromSeconds(j * j * 0.1));
                            }
                            else
                            {
                                // Ficou sem cache
                                Log.Error(ex);
                            }
                        }
                    }


                    var cacheWriteTime = sw.ElapsedMilliseconds;

                    if (!noWait)
                    {
                        _lastWebFetch = moment;
                    }

                    Log.DebugFormat("...Obtido da web em {0}ms e escrito em cache em {1}ms.", webTime, cacheWriteTime);
                    return new WebContent(content) { Moment = moment };
                }
                catch (WebException ex)
                {
                    Log.Warn(ex);
                    if (ex.Status == WebExceptionStatus.ProtocolError)
                    {
                        var response = ex.Response as HttpWebResponse;
                        if (response?.StatusCode == HttpStatusCode.NotFound)
                        {
                            throw;
                        }
                    }

                    if (i < MaxTry - 1)
                    {
                        Log.Warn("...esperando antes de tentar novamente.");
                        Thread.Sleep(TimeSpan.FromSeconds(i * i * 2));
                    }

                    lastException = ex;
                }
            }

            throw lastException;
        }

        private void DeleteFromCache(string contentFileName)
        {
            var cacheFileFullPath = Path.Combine(_cacheDirectory, contentFileName);
            File.Delete(cacheFileFullPath);
        }

        private static bool ParsePlayerStatsPage(Player player, string statsPageContent, out ParsePlayerError error)
        {
            if (statsPageContent.Length < 1024)
            {
                // Não tem como ter uma resposta válida com menos de 1k
                Log.WarnFormat("...conteúdo da página com apenas {0} bytes.", statsPageContent.Length);
                error = ParsePlayerError.SmallPage;
                return false;
            }

            var ci = CultureInfo.InvariantCulture;

            {
                #region Obter Estatisticas Gerais

                var count = 1;
                foreach (Match match in RegexWotInfoPlayerStat.Matches(statsPageContent))
                {
                    var row = (count - 1) / 3;
                    var column = count % 3;
                    var s = match.Groups[1].Value;
                    var d = 0.0;
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        try
                        {
                            d = double.Parse(s, NumberStyles.Any, ci);
                        }
                        catch (Exception)
                        {
                            d = 0;
                        }
                    }

                    switch (row)
                    {
                        case 0:
                            switch (column)
                            {
                                case 0:
                                    player.TotalBattles = (int)d;
                                    break;
                                case 1:
                                    //player.WeekBattles = (int) d;
                                    break;
                                case 2:
                                    player.MonthBattles = (int)d;
                                    break;
                            }

                            break;
                        case 1:
                            switch (column)
                            {
                                case 0:
                                    player.TotalWinRate = d / 100.0;
                                    break;
                                case 1:
                                    //player.WeekWinRate = d/100.0;
                                    break;
                                case 2:
                                    player.MonthWinRate = d / 100.0;
                                    break;
                            }

                            break;
                        case 2:
                            switch (column)
                            {
                                case 0:
                                    //player.TotalAvgDmg = d;
                                    break;
                                case 1:
                                    //player.WeekAvgDmg = d;
                                    break;
                                case 2:
                                    //player.MonthAvgDmg = d;
                                    break;
                            }

                            break;
                        case 3:
                            switch (column)
                            {
                                case 0:
                                    //player.TotalAvgRatio = d;
                                    break;
                                case 1:
                                    //player.WeekAvgRatio = d;
                                    break;
                                case 2:
                                    //player.MonthAvgRatio = d;
                                    break;
                            }

                            break;
                        case 4:
                            switch (column)
                            {
                                case 0:
                                    //player.TotalAvgDestroyed = d;
                                    break;
                                case 1:
                                    //player.WeekAvgDestroyed = d;
                                    break;
                                case 2:
                                    //player.MonthAvgDestroyed = d;
                                    break;
                            }

                            break;
                        case 8:
                            switch (column)
                            {
                                case 0:
                                    //player.TotalSurvived = d/100.0;
                                    break;
                                case 1:
                                    //player.WeekSurvived = d/100.0;
                                    break;
                                case 2:
                                    //player.MonthSurvived = d/100.0;
                                    break;
                            }

                            break;
                        case 9:
                            switch (column)
                            {
                                case 0:
                                    //player.TotalKillRatio = d;
                                    break;
                                case 1:
                                    //player.WeekKillRatio = d;
                                    break;
                                case 2:
                                    //player.MonthKillRatio = d;
                                    break;
                            }

                            break;
                        case 10:
                            switch (column)
                            {
                                case 0:
                                    //player.TotalAvgTier = d;
                                    break;
                                case 1:
                                    //player.WeekAvgTier = d;
                                    break;
                                case 2:
                                    //player.MonthAvgTier = d;
                                    break;
                            }

                            break;
                    }

                    ++count;
                }

                #endregion

                if (count == 1)
                {
                    // A API do WoT pode estar com problema, as vezes transiente
                    if (statsPageContent.Contains("WOT API is down"))
                    {
                        error = ParsePlayerError.WgApiIsDown;
                        return false;
                    }
                }

                #region Obter Os Totalizadores

                count = 1;
                foreach (Match match in RegexWotInfoPlayerTotal.Matches(statsPageContent))
                {
                    var row = (count - 1) / 3;
                    var column = count % 3;
                    var s = match.Groups[1].Value;
                    var d = 0.0;
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        try
                        {
                            d = double.Parse(s, NumberStyles.Any, ci);
                        }
                        catch (Exception)
                        {
                            d = 0;
                        }
                    }

                    switch (row)
                    {
                        case 0:
                            switch (column)
                            {
                                case 0:
                                    //player.TotalEfficiency = d;
                                    break;
                                case 1:
                                    //player.WeekEfficiency = d;
                                    break;
                                case 2:
                                    //player.MonthEfficiency = d;
                                    break;
                            }

                            break;
                        case 1:
                            switch (column)
                            {
                                case 0:
                                    //player.TotalWn7 = d;
                                    break;
                                case 1:
                                    //player.WeekWn7 = d;
                                    break;
                                case 2:
                                    //player.MonthWn7 = d;
                                    break;
                            }

                            break;
                        case 2:
                            switch (column)
                            {
                                case 0:
                                    player.TotalWn8 = d;
                                    break;
                                case 1:
                                    //player.WeekWn8 = d;
                                    break;
                                case 2:
                                    player.MonthWn8 = d;
                                    break;
                            }

                            break;
                    }

                    ++count;
                }

                #endregion
            }

            error = ParsePlayerError.NoError;
            return true;
        }

        public void DeleteCacheForPlayer(long playerId)
        {
            DeleteFromCache($"WoTInfo.{playerId}.txt");
        }

        public IEnumerable<Clan> GetClans(Plataform plataform, int minNumberOfPlayers = 15)
        {
            if (minNumberOfPlayers < 4)
            {
                throw new ArgumentOutOfRangeException(nameof(minNumberOfPlayers), minNumberOfPlayers,
                    @"No mínimo 4 membros!");
            }

            Log.DebugFormat("Listando clãs na plataforma {0} com pelo menos {1} membros...", plataform,
                minNumberOfPlayers);
            string server;
            switch (plataform)
            {
                case Plataform.XBOX:
                    server = "api-xbox-console.worldoftanks.com";
                    break;
                case Plataform.PS:
                    server = "api-ps4-console.worldoftanks.com";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(plataform), plataform, null);
            }

            for (int pageNumber = 1, currentCount = 100; currentCount > 0; ++pageNumber)
            {
                string lisUrl =
                    $"https://{server}/wotx/clans/list/?application_id={ApplicationId}&fields=clan_id%2Ctag%2Cmembers_count&page_no={pageNumber}";

                var json =
                    GetContentSync($"ListClans.{plataform}.{pageNumber}.{DateTime.UtcNow:yyyy-MM-dd.HHmm}.json", lisUrl,
                        WebCacheAge, false, Encoding.UTF8).Content;
                var response = JsonConvert.DeserializeObject<ClansListResponse>(json);
                if (response.IsError)
                {
                    Log.Error(response.Error);
                    yield break;
                }

                if (response.Meta.Count <= 0)
                {
                    Log.DebugFormat("listagem de clãs encerrada na página {0}.", pageNumber);
                    yield break;
                }

                currentCount = (int)response.Meta.Count;

                // Os clãs vem em ordem de tamanho, então se o primeiro já for menor, paramos
                if (response.Clans[0].MembersCount < minNumberOfPlayers)
                {
                    Log.DebugFormat("Página {0} inicia com apenas {1} membros.", pageNumber,
                        response.Clans[0].MembersCount);
                    yield break;
                }

                foreach (var clan in response.Clans.Where(c => c.MembersCount >= minNumberOfPlayers))
                {
                    yield return new Clan(plataform, clan.ClanId, clan.Tag) { AllMembersCount = clan.MembersCount };
                }
            }
        }

        private IEnumerable<Clan> GetClans(Plataform plataform, IEnumerable<ClanPlataform> clans)
        {
            var clanPlataforms = clans as ClanPlataform[] ?? clans.ToArray();
            if (!clanPlataforms.Any())
            {
                yield break;
            }

            Log.DebugFormat("Procurando dados na plataforma {0}...", plataform);
            string server;
            switch (plataform)
            {
                case Plataform.XBOX:
                    server = "api-xbox-console.worldoftanks.com";
                    break;
                case Plataform.PS:
                    server = "api-ps4-console.worldoftanks.com";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(plataform), plataform, null);
            }

            var requestedClans = new HashSet<long>(clanPlataforms.Select(c => c.ClanId));

            // Verifica se algum dos clãs foi encerrado
            string disbandedUrl =
                $"https://{server}/wotx/clans/info/?application_id={ApplicationId}&fields=clan_id%2Cis_clan_disbanded&clan_id={string.Join("%2C", requestedClans.Select(id => id.ToString()))}";
            var json =
                GetContentSync($"DisbandedClan.{plataform}.{DateTime.UtcNow:yyyy-MM-dd.HHmm}.json", disbandedUrl,
                    WebCacheAge, false, Encoding.UTF8).Content;
            var response = JsonConvert.DeserializeObject<ClansInfoResponse>(json);
            if (response.IsError)
            {
                Log.Error(response.Error);
                Log.ErrorFormat("Error on Request: {0}", disbandedUrl);
                yield break;
            }

            var disbandedClans =
                new HashSet<long>(response.Clans.Where(apiClanKv => apiClanKv.Value.IsDisbanded).Select(kv => kv.Key));
            Log.WarnFormat("{0} Clãs foram desfeitos.", disbandedClans.Count);

            // Tira os debandados para não dar pau na serialização
            requestedClans.ExceptWith(disbandedClans);

            // Pega os dados normais
            string requestUrl = $"https://{server}/wotx/clans/info/?application_id={ApplicationId}&clan_id=" +
                                $"{string.Join("%2C", requestedClans.Select(id => id.ToString()))}&fields=clan_id%2Ctag%2Cname%2Cmembers_count%2Ccreated_at%2Cis_clan_disbanded" +
                                "%2Cmembers%2Cmembers.role%2Cmembers.account_name&extra=members";

            json = GetContentSync($"InfoClan.{plataform}.{DateTime.UtcNow:yyyy-MM-dd.HHmm}.json", requestUrl,
                WebCacheAge, false, Encoding.UTF8).Content;

            response = JsonConvert.DeserializeObject<ClansInfoResponse>(json);
            if (response.IsError && response.Error?.Code == 504)
            {
                Log.Error(response.Error);
                Log.ErrorFormat("Error on Request: {0}", requestUrl);
                yield break;
            }

            foreach (var apiClanKv in response.Clans)
            {
                if (apiClanKv.Value.IsDisbanded)
                {
                    Log.WarnFormat("Clã id {0} foi desfeito.", apiClanKv.Key);
                    continue;
                }

                var clan = new Clan(plataform, apiClanKv.Key, apiClanKv.Value.Tag, apiClanKv.Value.Name)
                {
                    CreatedAtUtc = apiClanKv.Value.CreatedAtUtc,
                    MembershipMoment = DateTime.UtcNow
                };

                if (apiClanKv.Value.Members.Any())
                {
                    foreach (var memberKv in apiClanKv.Value.Members)
                    {
                        clan.Add(new Player
                        {
                            Id = memberKv.Key,
                            Rank = memberKv.Value.Rank,
                            Plataform = plataform,
                            Name = memberKv.Value.Name
                        });
                    }
                }

                yield return clan;
            }

            foreach (var id in disbandedClans)
            {
                yield return new Clan(plataform, id, string.Empty)
                {
                    IsDisbanded = true
                };
            }
        }

        /// <summary>
        ///     Devolve a GT a partir do ID
        /// </summary>
        public string GetPlayerNameById(Plataform plataform, long id)
        {
            Log.DebugFormat("Procurando GT de {1} na plataforma {0}...", plataform, id);
            string server;
            switch (plataform)
            {
                case Plataform.XBOX:
                    server = "api-xbox-console.worldoftanks.com";
                    break;
                case Plataform.PS:
                    server = "api-ps4-console.worldoftanks.com";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(plataform), plataform, null);
            }

            string url =
                $"https://{server}/wotx/account/info/?application_id={ApplicationId}&account_id={id}&fields=nickname";
            var json = GetContentSync($"GamerTagById.{plataform}.{id}.json", url, WebCacheAge, false, Encoding.UTF8)
                .Content;

            var result = JObject.Parse(json);

            if ((string)result["status"] != "ok")
            {
                var error = result["error"];
                int code = (int)error["code"];
                string msg = (string)error["message"];
                Log.ErrorFormat("Erro de API {0}, '{1}' chamando {2}", code, msg, url);
                return null;
            }

            var count = (int)result["meta"]["count"];
            if (count < 1)
            {
                Log.WarnFormat("Não achado ninguém com id '{0}'.", id);
                return null;
            }

            var name = (string)result["data"][$"{id}"]["nickname"];
            Log.DebugFormat("...achado '{0}'.", name);
            return name;
        }

        /// <summary>
        ///     Obtem da API os clãs todos
        /// </summary>
        public IEnumerable<Clan> GetClans(IEnumerable<ClanPlataform> clans)
        {
            var clanPlataforms = clans as ClanPlataform[] ?? clans.ToArray();

            var xboxClans = clanPlataforms.Where(c => c.Plataform == Plataform.XBOX);
            var psClans = clanPlataforms.Where(c => c.Plataform == Plataform.PS);

            return GetClans(Plataform.XBOX, xboxClans).Concat(GetClans(Plataform.PS, psClans));
        }

        /// <summary>
        ///     Devolve o jogador a partir da Gamertag
        /// </summary>
        public Player GetPlayerByGamerTag(Plataform plataform, string gamerTag)
        {
            Log.DebugFormat("Procurando dados na plataforma {0}...", plataform);
            string server;
            switch (plataform)
            {
                case Plataform.XBOX:
                    server = "api-xbox-console.worldoftanks.com";
                    break;
                case Plataform.PS:
                    server = "api-ps4-console.worldoftanks.com";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(plataform), plataform, null);
            }

            string url = $"https://{server}/wotx/account/list/?application_id={ApplicationId}&search={gamerTag}";
            var json =
                GetContentSync($"AccountList.{plataform}.{gamerTag}.json", url, WebCacheAge, false, Encoding.UTF8)
                    .Content;
            var result = JObject.Parse(json);
            var count = (int)result["meta"]["count"];
            if (count < 1)
            {
                Log.WarnFormat("Não achado ninguém com gamer tag '{0}'.", gamerTag);
                return null;
            }

            if (count >= 1)
            {
                var suggested = (string)result["data"][0]["nickname"];
                if (!suggested.Equals(gamerTag, StringComparison.InvariantCultureIgnoreCase))
                {
                    Log.WarnFormat("Há {0} resultados para a gamer tag '{1}', mas o 1º é '{2}'.", count, gamerTag,
                        suggested);
                    return null;
                }
            }

            var player = new Player
            {
                Id = (long)result["data"][0]["account_id"],
                Name = gamerTag,
                Moment = DateTime.UtcNow,
                Plataform = plataform
            };

            // Acho o clã do cidadão
            url =
                $"https://{server}/wotx/clans/accountinfo/?application_id={ApplicationId}&account_id={player.Id}&extra=clan";
            json =
                GetContentSync($"ClansAccountinfo.{plataform}.{gamerTag}.json", url, WebCacheAge, false, Encoding.UTF8)
                    .Content;
            result = JObject.Parse(json);
            count = (int)result["meta"]["count"];
            if (count == 1)
            {
                try
                {
                    player.ClanId = (long)result["data"][player.Id.ToString()]["clan_id"];
                    player.ClanTag = (string)result["data"][player.Id.ToString()]["clan"]["tag"];
                }
                catch
                {
                    // Not a member of a clan
                    player.ClanId = null;
                    player.ClanTag = string.Empty;
                }
            }

            return player;
        }

        private enum ParsePlayerError
        {
            NoError = 0,
            SmallPage = 2,
            WgApiIsDown = 3
        }

        /// <summary>
        ///     Conteudo obtido na Web (ou cache dela)
        /// </summary>
        private class WebContent
        {
            public WebContent(string content)
            {
                Content = content;
                Moment = DateTime.UtcNow;
            }

            /// <summary>
            ///     O conteudo em si
            /// </summary>
            public string Content { get; }

            /// <summary>
            ///     O momento em que o dado foi pego
            /// </summary>
            public DateTime Moment { get; set; }
        }
    }
}