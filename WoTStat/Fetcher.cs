using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using log4net;
using Negri.Wot.Achievements;
using Negri.Wot.Diagnostics;
using Negri.Wot.Tanks;
using Negri.Wot.WgApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Tank = Negri.Wot.WgApi.Tank;

namespace Negri.Wot
{
    /// <summary>
    ///     Retrieve information from the web (and APIs)
    /// </summary>
    public class Fetcher 
    {
        private const int MaxTry = 10;

        private static readonly ILog Log = LogManager.GetLogger(typeof(Fetcher));

        /// <summary>
        /// The only and only HTTP Client
        /// </summary>
        private static readonly HttpClient HttpClient;

        private readonly string _cacheDirectory;
        
        private DateTime _lastWebFetch = DateTime.MinValue;

        static Fetcher()
        {
            HttpClient = new HttpClient();
            HttpClient.DefaultRequestHeaders.Clear();
            HttpClient.DefaultRequestHeaders.Add("user-agent", "WoTClansBr by JP Negri at negrijp _at_ gmail.com");
        }

        public Fetcher(string cacheDirectory = null)
        {
            _cacheDirectory = cacheDirectory ?? Path.GetTempPath();
        }

        /// <summary>
        ///     WG App ID
        /// </summary>
        /// <remarks>
        ///     The <c>Demo</c> api key does not work anymore
        /// </remarks>
        public string WargamingApplicationId { set; private get; } = "demo";

        /// <summary>
        ///     Administrative API key for the WoTClans site
        /// </summary>
        public string WotClansAdminApiKey { set; private get; } = "nope";

        /// <summary>
        ///     Cache Age
        /// </summary>
        public TimeSpan WebCacheAge { set; private get; } = TimeSpan.FromHours(23);

        /// <summary>
        ///     Min interval between external web calls
        /// </summary>
        public TimeSpan WebFetchInterval { set; private get; } = TimeSpan.Zero;


        /// <summary>
        ///     Get Diagnostics on the WoTClans website
        /// </summary>
        /// <returns></returns>
        public SiteDiagnostic GetSiteDiagnostic()
        {
            Log.Debug("Obtendo o diagnostico do site remoto");

            const string url = "https://wotclans.com.br/api/status";

            var json = GetContent($"SiteDiagnostic.{DateTime.UtcNow:yyyy-MM-dd.HHmmss}.json",
                $"{url}?apiAdminKey={WotClansAdminApiKey}", WebCacheAge,
                false, Encoding.UTF8);
            var siteDiagnostic = JsonConvert.DeserializeObject<SiteDiagnostic>(json);
            return siteDiagnostic;
        }

        public IEnumerable<Tank> GetTanks(Platform platform)
        {
            return GetTanks(platform, null);
        }

        public Wn8ExpectedValues GetXvmWn8ExpectedValues()
        {
            Log.Debug("Obtendo os WN8 da XVM");
            const string url = "https://static.modxvm.com/wn8-data-exp/json/wn8exp.json";
            var json = GetContent("Wn8XVM.json", url, WebCacheAge, false, Encoding.UTF8);

            var ev = new Wn8ExpectedValues();

            var j = JObject.Parse(json);
            var h = j["header"];

            Debug.Assert(h != null, nameof(h) + " != null");

            ev.Source = (string) h["source"];
            ev.Version = (string) h["version"];

            var d = j["data"];
            Debug.Assert(d != null, nameof(d) + " != null");

            foreach (var dd in d.Children())
                ev.Add(new Wn8TankExpectedValues
                {
                    TankId = (long) dd["IDNum"],
                    Def = (double) dd["expDef"],
                    Frag = (double) dd["expFrag"],
                    Spot = (double) dd["expSpot"],
                    Damage = (double) dd["expDamage"],
                    WinRate = (double) dd["expWinRate"] / 100.0
                });

            return ev;
        }

        public IEnumerable<TankPlayer> GetTanksForPlayer(long playerId, long? tankId = null, bool includeMedals = true)
        {
            Log.DebugFormat("Obtendo tanques do jogador {0}...", playerId);

            var requestUrl =
                $"https://api-console.worldoftanks.com/wotx/tanks/stats/?application_id={WargamingApplicationId}&account_id={playerId}";
            if (tankId.HasValue) requestUrl += $"&tank_id={tankId.Value}";

            var json =
                GetContent($"TanksStats.{playerId}.json", requestUrl, WebCacheAge, false,
                    Encoding.UTF8);
            var response = JsonConvert.DeserializeObject<TanksStatsResponse>(json);
            if (response.IsError)
            {
                Log.Error(response.Error);
                return Enumerable.Empty<TankPlayer>();
            }

            var list = new Dictionary<long, TankPlayer>();
            foreach (var tankPlayers in response.Players.Values)
                if (tankPlayers != null)
                    foreach (var tankPlayer in tankPlayers)
                        // Tanks with zero XP should be ignored, as very likely it's WG returning only the win rate
                        if (tankPlayer.All.XP > 0.0)
                            list.Add(tankPlayer.TankId, tankPlayer);

            if (!includeMedals) return list.Values;

            // Retrieve also the medals
            requestUrl =
                $"https://api-console.worldoftanks.com/wotx/tanks/achievements/?application_id={WargamingApplicationId}&account_id={playerId}";
            if (tankId.HasValue) requestUrl += $"&tank_id={tankId.Value}";

            json = GetContent($"TanksAchievements.{playerId}.json", requestUrl, WebCacheAge, false, Encoding.UTF8);
            var achievementsResponse = JsonConvert.DeserializeObject<TanksAchievementsResponse>(json);
            if (achievementsResponse.IsError)
            {
                Log.Error(achievementsResponse.Error);
                return list.Values;
            }

            var tanksAchievements = achievementsResponse.Players[playerId];

            if (tanksAchievements == null)
            {
                Log.Error($"No tanksAchievements for player {playerId}");
                return list.Values;
            }

            foreach (var ta in tanksAchievements)
                if (list.TryGetValue(ta.TankId, out var tankPlayer))
                {
                    tankPlayer.All.Ribbons = ta.Ribbons;
                    tankPlayer.All.Achievements = ta.Achievements;
                }

            return list.Values;
        }

        /// <summary>
        ///     Because the PC API has pagination
        /// </summary>
        [SuppressMessage("ReSharper", "StringLiteralTypo")]
        private IEnumerable<Tank> GetPcTanks(long? tankId)
        {
            var tanks = new List<Tank>();

            int page = 1, totalPages;
            do
            {
                var url =
                    $"https://api.worldoftanks.com/wot/encyclopedia/vehicles/?application_id={WargamingApplicationId}&fields=tank_id%2Ctier%2Ctype%2Cshort_name%2Ctag%2Cis_premium%2Cnation%2Cname&page_no={page}&limit=100";
                if (tankId.HasValue) url += $"&tank_id={tankId.Value}";

                var json = GetContent($"Vehicles.{Platform.PC}.{DateTime.UtcNow:yyyyMMddHH}.{page}.json", url,
                    WebCacheAge, false, Encoding.UTF8);
                var response = JsonConvert.DeserializeObject<VehiclesResponse>(json);
                if (response.IsError)
                {
                    Log.Error(response.Error);
                    return Enumerable.Empty<Tank>();
                }

                tanks.AddRange(response.Data.Values);

                totalPages = response.Meta.PageTotal;
                page += 1;
            } while (page <= totalPages);

            foreach (var tank in tanks) tank.Plataform = Platform.PC;

            return tanks;
        }

        [SuppressMessage("ReSharper", "StringLiteralTypo")]
        private IEnumerable<Tank> GetTanks(Platform platform, long? tankId)
        {
            Log.DebugFormat("Procurando dados de tanques para {0}...", platform);
            string server;
            switch (platform)
            {
                case Platform.Console:
                    server = "api-console.worldoftanks.com";
                    break;
                case Platform.PC:
                    return GetPcTanks(tankId);
                default:
                    throw new ArgumentOutOfRangeException(nameof(platform), platform, null);
            }

            var requestUrl =
                $"https://{server}/wotx/encyclopedia/vehicles/?application_id={WargamingApplicationId}&fields=tank_id%2Cname%2Cshort_name%2Cis_premium%2Ctier%2Ctag%2Ctype%2Cimages%2Cnation";
            if (tankId != null) requestUrl += $"&tank_id={tankId.Value}";

            var json =
                GetContent($"Vehicles.{platform}.{DateTime.UtcNow:yyyyMMddHH}.json", requestUrl, WebCacheAge,
                    false,
                    Encoding.UTF8);

            var response = JsonConvert.DeserializeObject<VehiclesResponse>(json);
            if (response.IsError)
            {
                Log.Error(response.Error);
                return Enumerable.Empty<Tank>();
            }

            foreach (var tank in response.Data.Values) tank.Plataform = platform;

            return response.Data.Values;
        }


        public Clan FindClan(Platform platform, string clanTag, bool returnSmallClans = false)
        {
            Log.DebugFormat("Procurando dados do cl� {0}@{1}...", clanTag, platform);
            string server;
            switch (platform)
            {
                case Platform.XBOX:
                    server = "api-console.worldoftanks.com";
                    break;
                case Platform.PS:
                    server = "api-console.worldoftanks.com";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(platform), platform, null);
            }

            var requestUrl =
                $"https://{server}/wotx/clans/list/?application_id={WargamingApplicationId}&search={clanTag}&limit=1";

            var json =
                GetContent($"FindClan.{platform}.{clanTag}.json", requestUrl, TimeSpan.FromMinutes(1), false,
                    Encoding.UTF8);

            var response = JsonConvert.DeserializeObject<ClansListResponse>(json);
            if (response.IsError)
            {
                Log.Error(response.Error);
                return null;
            }

            // Deve coincidir
            if (response.Clans.Length != 1)
            {
                Log.Error($"H� {response.Clans.Length} respostas na busca por {clanTag}.");
                return null;
            }

            var found = response.Clans[0];
            if (string.Compare(clanTag, found.Tag, StringComparison.OrdinalIgnoreCase) != 0)
            {
                Log.Error($"A busca por '{clanTag}' encontrou apenas '{found.Tag}', que n�o coincide");
                return null;
            }

            if ((found.MembersCount < 7) && (!returnSmallClans))
            {
                Log.Error(
                    $"A busca por '{clanTag}' encontrou apenas {found.MembersCount} membros no cl� id {found.ClanId}. Ele n�o ser� adicionado.");
                return null;
            }

            return new Clan(found.ClanId, found.Tag) {AllMembersCount = found.MembersCount};
        }


        private string GetContent(string cacheFileTitle, string url, TimeSpan maxCacheAge, bool noWait,
            Encoding encoding = null)
        {
            Log.DebugFormat("Getting '{0}' with cache key {1}...", url, cacheFileTitle);

            encoding = encoding ?? Encoding.UTF8;

            var cacheFileName = Path.Combine(_cacheDirectory, cacheFileTitle);
            if (!File.Exists(cacheFileName))
            {
                Log.Debug("...not on cache...");
                return GetContentFromWeb(cacheFileName, url, noWait, encoding);
            }

            var fi = new FileInfo(cacheFileName);
            var moment = fi.LastWriteTimeUtc;
            var age = DateTime.UtcNow - moment;
            if (age > maxCacheAge)
            {
                Log.DebugFormat("...cache file '{0}' from {1:yyyy-MM-dd HH:mm} expired with {2:N1}h...",
                    cacheFileTitle, moment, age.TotalHours);

                return GetContentFromWeb(cacheFileName, url, noWait, encoding);
            }

            Log.Debug("...retrieved from cache.");
            return File.ReadAllText(cacheFileName, encoding);
        }

        private string GetContentFromWeb(string cacheFileName, string url, bool noWait, Encoding encoding)
        {
            var timeSinceLastFetch = DateTime.UtcNow - _lastWebFetch;
            var waitTime = WebFetchInterval - timeSinceLastFetch;
            var waitTimeMs = Math.Max((int) waitTime.TotalMilliseconds, 0);
            if (!noWait & (waitTimeMs > 100))
            {
                Log.DebugFormat("...waiting {0:N1}s to use the web...", waitTimeMs / 1000.0);
                Thread.Sleep(waitTimeMs);
            }

            Exception lastException = new ApplicationException("Code flow error!");

            for (var i = 0; i < MaxTry; ++i)
                try
                {
                    var moment = DateTime.UtcNow;
                    var sw = Stopwatch.StartNew();

                    var content = HttpClient.GetStringAsync(url).Result;

                    if (!noWait) _lastWebFetch = moment;
                    var webTime = sw.ElapsedMilliseconds;

                    // Escreve em cache
                    sw.Restart();

                    for (var j = 0; j < MaxTry; ++j)
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
                                Log.Warn("...waiting before retry...");
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

                    Log.DebugFormat("...retrieved from web in {0}ms and wrote to cache in {1}ms.", webTime, cacheWriteTime);
                    return content;
                }
                catch (WebException ex)
                {
                    Log.Warn(ex);
                    if (ex.Status == WebExceptionStatus.ProtocolError)
                    {
                        var response = ex.Response as HttpWebResponse;
                        if (response?.StatusCode == HttpStatusCode.NotFound) throw;
                    }

                    if (i < MaxTry - 1)
                    {
                        Log.Warn("...waiting before retry.");
                        Thread.Sleep(TimeSpan.FromSeconds(i * i * 2));
                    }

                    lastException = ex;
                }

            throw lastException;
        }

        /// <summary>
        ///     Retrieve basic information about clans in the game
        /// </summary>
        /// <param name="minNumberOfPlayers">The minimum number of players to return</param>
        /// <returns></returns>
        [SuppressMessage("ReSharper", "StringLiteralTypo")]
        public IEnumerable<Clan> GetClans(int minNumberOfPlayers = 7)
        {
            Log.DebugFormat("Listing clans with at least {0} members...", minNumberOfPlayers);

            for (int pageNumber = 1, currentCount = 100; currentCount > 0; ++pageNumber)
            {
                var lisUrl =
                    $"https://api-console.worldoftanks.com/wotx/clans/list/?application_id={WargamingApplicationId}&fields=clan_id%2Ctag%2Cmembers_count&page_no={pageNumber}";

                var json =
                    GetContent($"ListClans.{pageNumber}.{DateTime.UtcNow:yyyy-MM-dd}.json", lisUrl,
                        WebCacheAge, false, Encoding.UTF8);
                var response = JsonConvert.DeserializeObject<ClansListResponse>(json);
                if (response.IsError)
                {
                    Log.Error(response.Error);
                    yield break;
                }

                if (response.Meta.Count <= 0)
                {
                    Log.DebugFormat("Clans listing finished on page {0}.", pageNumber);
                    yield break;
                }

                currentCount = (int) response.Meta.Count;

                // Os cl�s vem em ordem de tamanho, ent�o se o primeiro j� for menor, paramos
                if (response.Clans[0].MembersCount < minNumberOfPlayers)
                {
                    Log.DebugFormat("Page {0} starts with clans having {1} members.", pageNumber,
                        response.Clans[0].MembersCount);
                    yield break;
                }

                foreach (var clan in response.Clans.Where(c => c.MembersCount >= minNumberOfPlayers))
                    yield return new Clan(clan.ClanId, clan.Tag) {AllMembersCount = clan.MembersCount};
            }
        }

        public IEnumerable<Clan> GetClans(IEnumerable<ClanBaseInformation> clans)
        {
            var c = clans.ToArray();
            if (c.Length <= 100)
            {
                return GetClansInternal(c);
            }

            // Page calls to API in 100 ids
            var r = new List<Clan>(c.Length);
            var pages = c.Length / 100 + 1;
            for (var p = 0; p < pages; ++p)
            {
                var call = c.Skip(p * 100).Take(100);
                r.AddRange(GetClansInternal(call));
            }

            return r;
        }

        /// <summary>
        ///     Get All information about the clans
        /// </summary>
        [SuppressMessage("ReSharper", "StringLiteralTypo")]
        public IEnumerable<Clan> GetClansInternal(IEnumerable<ClanBaseInformation> clans)
        {
            var clanArray = clans as ClanBaseInformation[] ?? clans.ToArray();
            if (!clanArray.Any())
            {
                yield break;
            }

            Log.Debug($"Searching {clanArray.Length} clan's details...");

            var requestedClans = new HashSet<long>(clanArray.Select(c => c.ClanId));

            // Verifica se algum dos cl�s foi encerrado
            var disbandedUrl =
                $"https://api-console.worldoftanks.com/wotx/clans/info/?application_id={WargamingApplicationId}&fields=clan_id%2Cis_clan_disbanded&clan_id={string.Join("%2C", requestedClans.Select(id => id.ToString()))}";
            var json =
                GetContent($"DisbandedClan.{DateTime.UtcNow:yyyy-MM-dd.HHmmss}.json", disbandedUrl,
                    WebCacheAge, false, Encoding.UTF8);
            var response = JsonConvert.DeserializeObject<ClansInfoResponse>(json);
            if (response.IsError)
            {
                Log.Error(response.Error);
                Log.ErrorFormat("Error on Request: {0}", disbandedUrl);
                yield break;
            }

            var disbandedClans =
                new HashSet<long>(response.Clans.Where(apiClanKv => apiClanKv.Value.IsDisbanded).Select(kv => kv.Key));
            Log.WarnFormat("{0} clans where disbanded.", disbandedClans.Count);

            // Clear the disbanded so deserialization works
            requestedClans.ExceptWith(disbandedClans);

            // Pega os dados normais
            var requestUrl =
                $"https://api-console.worldoftanks.com/wotx/clans/info/?application_id={WargamingApplicationId}&clan_id=" +
                $"{string.Join("%2C", requestedClans.Select(id => id.ToString()))}&fields=clan_id%2Ctag%2Cname%2Cmembers_count%2Ccreated_at%2Cis_clan_disbanded" +
                "%2Cmembers%2Cmembers.role%2Cmembers.account_name&extra=members";

            json = GetContent($"InfoClan.{DateTime.UtcNow:yyyy-MM-dd.HHmmss}.json", requestUrl,
                WebCacheAge, false, Encoding.UTF8);

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
                    Log.WarnFormat("Clan id {0} was disbanded.", apiClanKv.Key);
                    continue;
                }

                var clan = new Clan(apiClanKv.Key, apiClanKv.Value.Tag, apiClanKv.Value.Name)
                {
                    CreatedAtUtc = apiClanKv.Value.CreatedAtUtc,
                    MembershipMoment = DateTime.UtcNow
                };

                if (apiClanKv.Value.Members.Any())
                    foreach (var memberKv in apiClanKv.Value.Members)
                    {
                        var pn = NormalizeNickname(memberKv.Value.Name);

                        clan.Add(new Player
                        {
                            Id = memberKv.Key,
                            Rank = memberKv.Value.Rank,
                            Platform = pn.platform,
                            Name = pn.name
                        });
                    }

                yield return clan;
            }

            foreach (var id in disbandedClans)
            {
                yield return new Clan(id, string.Empty)
                {
                    IsDisbanded = true
                };
            }
        }

        private static (Platform platform, string name) NormalizeNickname(string nickname)
        {
            if (string.IsNullOrWhiteSpace(nickname))
            {
                return (Platform.Console, string.Empty);
            }

            if (nickname.EndsWith("-p"))
            {
                return (Platform.PS, nickname.Substring(0, nickname.Length - 2));
            }

            if (nickname.EndsWith("-x"))
            {
                return (Platform.XBOX, nickname.Substring(0, nickname.Length - 2));
            }

            return (Platform.Console, nickname);
        }

        /// <summary>
        ///     Retrieve a GT given a player ID
        /// </summary>
        public string GetPlayerNameById(Platform platform, long id)
        {
            Log.DebugFormat("Procurando GT de {1} na plataforma {0}...", platform, id);
            string server;
            switch (platform)
            {
                case Platform.XBOX:
                    server = "api-console.worldoftanks.com";
                    break;
                case Platform.PS:
                    server = "api-console.worldoftanks.com";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(platform), platform, null);
            }

            var url =
                $"https://{server}/wotx/account/info/?application_id={WargamingApplicationId}&account_id={id}&fields=nickname";
            var json = GetContent($"GamerTagById.{platform}.{id}.json", url, WebCacheAge, false, Encoding.UTF8);

            var result = JObject.Parse(json);

            if ((string) result["status"] != "ok")
            {
                var error = result["error"];
                var code = (int) error["code"];
                var msg = (string) error["message"];
                Log.ErrorFormat("Erro de API {0}, '{1}' chamando {2}", code, msg, url);
                return null;
            }

            var count = (int) result["meta"]["count"];
            if (count < 1)
            {
                Log.WarnFormat("N�o achado ningu�m com id '{0}'.", id);
                return null;
            }

            var name = (string) result["data"][$"{id}"]["nickname"];
            Log.DebugFormat("...achado '{0}'.", name);
            return name;
        }

        [SuppressMessage("ReSharper", "StringLiteralTypo")]
        public Player GetPlayerByGamerTag(Platform platform, string gamerTag)
        {
            Log.DebugFormat("Procurando dados na plataforma {0}...", platform);
            string server;
            switch (platform)
            {
                case Platform.XBOX:
                    server = "api-console.worldoftanks.com";
                    break;
                case Platform.PS:
                    server = "api-console.worldoftanks.com";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(platform), platform, null);
            }

            var url = $"https://{server}/wotx/account/list/?application_id={WargamingApplicationId}&search={gamerTag}";
            var json =
                GetContent($"AccountList.{platform}.{gamerTag}.json", url, WebCacheAge, false, Encoding.UTF8);
            var result = JObject.Parse(json);
            if ((string) result["status"] == "error")
            {
                Log.WarnFormat("Erro na busca: {0}", (string) result["error"]["message"]);
                return null;
            }

            var count = (int) result["meta"]["count"];
            if (count < 1)
            {
                Log.WarnFormat("N�o achado ningu�m com gamer tag '{0}'.", gamerTag);
                return null;
            }

            if (count >= 1)
            {
                var suggested = (string) result["data"][0]["nickname"];
                if (!suggested.Equals(gamerTag, StringComparison.InvariantCultureIgnoreCase))
                {
                    Log.WarnFormat("H� {0} resultados para a gamer tag '{1}', mas o 1� � '{2}'.", count, gamerTag,
                        suggested);
                    return null;
                }
            }

            var player = new Player
            {
                Id = (long) result["data"][0]["account_id"],
                Name = gamerTag,
                Moment = DateTime.UtcNow,
                Platform = platform
            };

            // Acho o cl� do cidad�o
            url =
                $"https://{server}/wotx/clans/accountinfo/?application_id={WargamingApplicationId}&account_id={player.Id}&extra=clan";
            json =
                GetContent($"ClansAccountinfo.{platform}.{gamerTag}.json", url, WebCacheAge, false, Encoding.UTF8);
            result = JObject.Parse(json);
            count = (int) result["meta"]["count"];
            if (count == 1)
                try
                {
                    player.ClanId = (long) result["data"][player.Id.ToString()]["clan_id"];
                    player.ClanTag = (string) result["data"][player.Id.ToString()]["clan"]["tag"];
                }
                catch
                {
                    // Not a member of a clan
                    player.ClanId = null;
                    player.ClanTag = string.Empty;
                }

            return player;
        }

        /// <summary>
        ///     Get all medals on the game
        /// </summary>
        public IEnumerable<Medal> GetMedals()
        {
            Log.Debug("Fetching medals on platform...");
            

            var url = $"https://api-console.worldoftanks.com/wotx/encyclopedia/achievements/?application_id={WargamingApplicationId}";
            var json = GetContent($"Achievements.json", url, WebCacheAge, false, Encoding.UTF8);

            var o = JObject.Parse(json);

            var status = (string) o["status"];
            if (status != "ok")
            {
                var error = o["error"];
                Log.Error($"Error: {(string) error["code"]} - {(string) error["message"]}");
                throw new ApplicationException(
                    $"Error calling WG API: {(string) error["code"]} - {(string) error["message"]}");
            }

            var medals = new List<Medal>();

            var data = o["data"];
            foreach (var t in data.Cast<JProperty>())
            {
                var ti = t.Value;

                var medal = new Medal
                {
                    Platform = Platform.Console,
                    Code = t.Name,
                    Category = CategoryExtensions.Parse((string) ti["category"]),
                    Name = (string) ti["name"],
                    Section = SectionExtensions.Parse((string) ti["section"]),
                    Type = TypeExtensions.Parse((string) ti["type"]),
                    Description = (string) ti["description"],
                    HeroInformation = (string) ti["hero_info"],
                    Condition = (string) ti["condition"]
                };

                medals.Add(medal);
            }

            return medals;
        }
    }
}