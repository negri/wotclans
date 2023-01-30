using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using log4net;
using Negri.Wot.Models;
using Negri.Wot.Sql;
using Negri.Wot.Tanks;

namespace Negri.Wot.Site.Controllers
{
    public class PlayersController : Controller
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(PlayersController));

        public ActionResult Overall(string clanName, long playerId)
        {
            var getter = HttpRuntime.Cache.Get("FileGetter", GlobalHelper.CacheMinutes,
                () => new FileGetter(GlobalHelper.DataFolder));

            var clan = getter.GetClan(clanName);
            if (clan == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound, $"Can't find the clan [{clanName}] on this server.");
            }

            var player = clan.Get(playerId);
            if (player == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound, $"Can't find a player with id {playerId} on the clan [{clanName}].");
            }

            string url;
            switch (player.Origin)
            {
                case PlayerDataOrigin.WotStatConsole:
                    url = GetWoTStatConsoleOverallUrl(player);
                    break;
                case PlayerDataOrigin.WotInfo:
                    url = GetWoTInfoOverallUrl(player);
                    break;
                default:
                    url = GetDefaultOverallUrl(player);
                    break;
            }

            if (GlobalHelper.UseExternalPlayerPage)
            {
                return Redirect(url);
            }

            try
            {

                var connectionString = ConfigurationManager.ConnectionStrings["Store"].ConnectionString;
                var db = new KeyStore(connectionString);
                var fullPlayer = db.GetPlayer(playerId);

                if (fullPlayer == null)
                {
                    Log.Warn($"Overall({clanName}, {playerId}): fullPlayer == null");
                    return Redirect(url);
                }

                var wn8ExpectedValues = getter.GetTanksWN8ReferenceValues();
                if (wn8ExpectedValues == null)
                {
                    Log.Error($"Overall({clanName}, {playerId}): wn8ExpectedValues == null");
                    return Redirect(url);
                }

                var moes = getter.GetTanksMoe();

                if (fullPlayer.Performance == null)
                {
                    Log.Warn($"Overall({clanName}, {playerId}): fullPlayer.Performance == null");
                    return Redirect(url);
                }

                if (fullPlayer.Performance.All == null)
                {
                    Log.Error($"Overall({clanName}, {playerId}): fullPlayer.Performance.All == null");
                    return Redirect(url);
                }

                if (fullPlayer.Performance.Month == null)
                {
                    Log.Error($"Overall({clanName}, {playerId}): fullPlayer.Performance.Month == null");
                    return Redirect(url);
                }

                if (fullPlayer.Performance.Week == null)
                {
                    Log.Error($"Overall({clanName}, {playerId}): fullPlayer.Performance.Week == null");
                    return Redirect(url);
                }

                fullPlayer.Calculate(wn8ExpectedValues);

                var allLeaderBoard = getter.GetTankLeaders().ToArray();
                var onLeaderboard = allLeaderBoard.Where(l => l.PlayerId == playerId).ToArray();
                var onLeaderBoardTanks = new HashSet<long>(onLeaderboard.Select(l => l.TankId));
                var worstLeaders = allLeaderBoard.Where(l => l.Order == 25 && fullPlayer.HasTank(l.TankId) && !onLeaderBoardTanks.Contains(l.TankId)).ToDictionary(l => l.TankId);

                var leaderIfPlayABattle = fullPlayer.Performance.All.Values
                    .Where(t => (t.Tier >= 5) && (t.Battles >= t.Tier * 10) && (worstLeaders.ContainsKey(t.TankId)) &&
                                (t.TotalDamagePerBattle > worstLeaders[t.TankId].TotalDamage) && (t.LastBattleAge.TotalDays > 28 * 3))
                    .OrderByDescending(t => t.Tier).ThenBy(t => t.LastBattle).ToArray();

                var leaderIfFewMoreGames = fullPlayer.Performance.All.Values
                    .Where(t => (t.Tier >= 5) && (t.Battles >= (t.Tier * 10) - 10) && (t.Battles < (t.Tier * 10)) && (worstLeaders.ContainsKey(t.TankId)) &&
                                (t.TotalDamagePerBattle > worstLeaders[t.TankId].TotalDamage) && (t.LastBattleAge.TotalDays <= 28 * 3))
                    .OrderByDescending(t => t.Tier).ThenBy(t => t.LastBattle).ToArray();

                var leaderIfFewMoreDamage = fullPlayer.Performance.All.Values
                    .Where(t => (t.Tier >= 5) && (t.Battles >= (t.Tier * 10)) && (worstLeaders.ContainsKey(t.TankId)) &&
                                (t.TotalDamagePerBattle * 1.05 > worstLeaders[t.TankId].TotalDamage) &&
                                (t.TotalDamagePerBattle < worstLeaders[t.TankId].TotalDamage) &&
                                (t.LastBattleAge.TotalDays <= 28 * 3))
                    .OrderByDescending(t => t.Tier).ThenBy(t => t.LastBattle).ToArray();

                var leaderOnNextUpdate = fullPlayer.Performance.All.Values
                    .Where(t => (t.Tier >= 5) && (t.Battles >= (t.Tier * 10)) && (worstLeaders.ContainsKey(t.TankId)) &&
                                (t.TotalDamagePerBattle > worstLeaders[t.TankId].TotalDamage) && (t.LastBattleAge.TotalDays <= 28 * 3))
                    .OrderByDescending(t => t.Tier).ThenBy(t => t.LastBattle).ToArray();

                var model = new PlayerPage
                {
                    Clan = clan,
                    Player = fullPlayer,
                    Wn8ExpectedValues = wn8ExpectedValues,
                    MoEs = moes,
                    OnLeaderboard = onLeaderboard,
                    LeaderIfPlayABattle = leaderIfPlayABattle,
                    LeaderIfFewMoreGames = leaderIfFewMoreGames,
                    LeaderIfFewMoreDamage = leaderIfFewMoreDamage,
                    LeaderOnNextUpdate = leaderOnNextUpdate,
                    ExternalUrl = url,

                    WoTStatConsoleOverallUrl = GetWoTStatConsoleOverallUrl(player),
                    WotStatConsoleRecentUrl = GetWoTStatConsoleRecentUrl(player),
                    WotStatConsoleHistoryUrl = GetWoTStatConsoleHistoryUrl(player),
                    WotStatConsoleClanUrl = GetWotStatConsoleClan(clan),

                    WoTInfoOverallUrl = GetWoTInfoOverallUrl(player),
                    WoTInfoRecentUrl = GetWoTInfoRecentUrl(player),
                    WoTInfoHistoryUrl = GetWoTInfoHistoryUrl(player),

                    WoTConsoleRuOverallUrl = GetWoTConsoleRuOverallUrl(player),
                    WotConsoleRuClanUrl = GetWotConsoleRuClanUrl(clan),

                    WoTStarsOverallUrl = GetWoTStarsOverallUrl(player),
                    WoTStarsClanUrl = GetWoTStarsClanUrl(clan),
                };

                return View(model);
            }
            catch (Exception ex)
            {
                Log.Error($"Overall({clanName}, {playerId})", ex);
                return Redirect(url);
            }
        }

        


        private string GetWoTStarsClanUrl(Clan clan)
        {   
            // https://www.wotstars.com/clans/1073753199
            return $"https://www.wotstars.com/clans/{clan.ClanId}";
        }

        private string GetWoTStarsOverallUrl(Player player)
        {
            // https://www.wotstars.com/xbox/1763298
            // https://www.wotstars.com/ps/1074422202

            string platform;
            switch (player.Platform)
            {
                case Platform.XBOX:
                    platform = "xbox";
                    break;
                case Platform.PS:
                    platform = "ps";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return $"https://www.wotstars.com/{platform}/{player.Id}";
        }

        private string GetWotConsoleRuClanUrl(Clan clan)
        {
            // https://www.wotconsole.info/clan/?n=SELVA
            return $"https://www.wotconsole.info/clan/?n={clan.ClanTag.ToUpperInvariant()}";
        }

        private string GetWoTConsoleRuOverallUrl(Player player)
        {
            // https://www.wotconsole.info/player/?p=ps4&n=juckof
            // https://www.wotconsole.info/player/?p=xbox&n=JP+Negri+Coder

            string platform;
            switch (player.Platform)
            {
                case Platform.XBOX:
                    platform = "xbox";
                    break;
                case Platform.PS:
                    platform = "ps4";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var name = player.Name.Replace(' ', '+');
            return $"https://www.wotconsole.info/player/?p={platform}&n={name}";
        }


        private static string GetDefaultOverallUrl(Player player)
        {
            switch (GlobalHelper.DefaultPlayerDetails)
            {
                case PlayerDataOrigin.WotInfo:
                    return GetWoTInfoOverallUrl(player);
                case PlayerDataOrigin.WotStatConsole:
                    return GetWoTStatConsoleOverallUrl(player);
                default:
                    // As WoTInfo appears to be more stable...
                    return GetWoTInfoOverallUrl(player);
            }
        }

        private string GetWotStatConsoleClan(Clan clan)
        {
            // https://wotcstat.info/clandetails?Tag=11458&lang=en

            string lang = GlobalHelper.Language;
            string externalLang;
            switch (lang)
            {
                case "pt":
                    externalLang = "pt";
                    break;
                case "pl":
                    externalLang = "pl";
                    break;
                case "de":
                    externalLang = "de";
                    break;
                case "it":
                    externalLang = "it";
                    break;
                case "es":
                    externalLang = "es";
                    break;
                default:
                    externalLang = "en";
                    break;
            }

            return $"https://wotcstat.info/clandetails?Tag={clan.ClanId}&lang={externalLang}";
        }

        private static string GetWoTStatConsoleOverallUrl(Player player)
        {
            // https://wotcstat.info/player?id=938687&s=xbox&l=en

            string lang = GlobalHelper.Language;
            string externalLang;
            switch (lang)
            {
                case "pt":
                    externalLang = "pt";
                    break;
                case "pl":
                    externalLang = "pl";
                    break;
                case "de":
                    externalLang = "de";
                    break;
                case "it":
                    externalLang = "it";
                    break;
                case "es":
                    externalLang = "es";
                    break;
                default:
                    externalLang = "en";
                    break;
            }

            var p = player.Platform == Platform.PS ? "ps4" : "xbox";

            var url =
                $"https://wotcstat.info/player?id={player.Id}&s={p}&l={externalLang}";
            
            return url;
        }


        private static string GetWoTInfoOverallUrl(Player player)
        {
            // http://wotinfo.net/en/efficiency?playername=JP+Negri+Coder&playerid=1763298&server=XBOX
            // http://wotinfo.net/de/efficiency?server=PS4&playername=docilein

            var lang = GlobalHelper.Language;
            string externalLang;
            switch (lang)
            {
                case "de":
                    externalLang = "de";
                    break;
                case "ru":
                    externalLang = "ru";
                    break;
                default:
                    externalLang = "en";
                    break;
            }

            var p = player.Platform == Platform.PS ? "PS4" : "XBOX";

            var url = $"http://wotinfo.net/{externalLang}/efficiency?playername={player.Name.Replace(' ', '+')}&playerid={player.Id}&server={p}";
            return url;
        }

        private static string GetWoTInfoHistoryUrl(Player player)
        {
            // http://wotinfo.net/en/trend?playerid=1763298&server=XBOX

            var lang = GlobalHelper.Language;
            string externalLang;
            switch (lang)
            {
                case "de":
                    externalLang = "de";
                    break;
                case "ru":
                    externalLang = "ru";
                    break;
                default:
                    externalLang = "en";
                    break;
            }

            var p = player.Platform == Platform.PS ? "PS4" : "XBOX";

            return $"http://wotinfo.net/{externalLang}/trend?playerid={player.Id}&server={p}";
        }

        public ActionResult Recent(string clanName, long playerId)
        {
            var getter = HttpRuntime.Cache.Get("FileGetter", GlobalHelper.CacheMinutes,
                () => new FileGetter(GlobalHelper.DataFolder));

            var clan = getter.GetClan(clanName);
            if (clan == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound, $"Can't find the clan [{clanName}] on this server.");
            }

            var player = clan.Get(playerId);
            if (player == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound, $"Can't find a player with id {clanName} on this server.");
            }

            string url;
            switch (player.Origin)
            {
                case PlayerDataOrigin.WotStatConsole:
                    url = GetWoTStatConsoleRecentUrl(player);
                    break;
                case PlayerDataOrigin.WotInfo:
                    url = GetWoTInfoRecentUrl(player);
                    break;
                default:
                    url = GetDefaultRecentUrl(player);
                    break;
            }

            if (GlobalHelper.UseExternalPlayerPage)
            {
                return Redirect(url);
            }

            try
            {

                var connectionString = ConfigurationManager.ConnectionStrings["Store"].ConnectionString;
                var db = new KeyStore(connectionString);
                var fullPlayer = db.GetPlayer(playerId);

                if (fullPlayer == null)
                {
                    Log.Warn($"Recent({clanName}, {playerId}): fullPlayer == null");
                    return Redirect(url);
                }

                var wn8ExpectedValues = getter.GetTanksWN8ReferenceValues();
                if (wn8ExpectedValues == null)
                {
                    Log.Error($"Recent({clanName}, {playerId}): wn8ExpectedValues == null");
                    return Redirect(url);
                }

                var moes = getter.GetTanksMoe();

                if (fullPlayer.Performance == null)
                {
                    Log.Error($"Recent({clanName}, {playerId}): fullPlayer.Performance == null");
                    return Redirect(url);
                }

                if (fullPlayer.Performance.All == null)
                {
                    Log.Error($"Recent({clanName}, {playerId}): fullPlayer.Performance.All == null");
                    return Redirect(url);
                }

                if (fullPlayer.Performance.Month == null)
                {
                    Log.Error($"Recent({clanName}, {playerId}): fullPlayer.Performance.Month == null");
                    return Redirect(url);
                }

                if (fullPlayer.Performance.Week == null)
                {
                    Log.Error($"Recent({clanName}, {playerId}): fullPlayer.Performance.Week == null");
                    return Redirect(url);
                }

                fullPlayer.Calculate(wn8ExpectedValues);
                
                var model = new PlayerPage
                {
                    Clan = clan,
                    Player = fullPlayer,
                    Wn8ExpectedValues = wn8ExpectedValues,
                    MoEs = moes,
                    ExternalUrl = url,

                    WoTStatConsoleOverallUrl = GetWoTStatConsoleOverallUrl(player),
                    WotStatConsoleRecentUrl = GetWoTStatConsoleRecentUrl(player),
                    WotStatConsoleHistoryUrl = GetWoTStatConsoleHistoryUrl(player),
                    WotStatConsoleClanUrl = GetWotStatConsoleClan(clan),

                    WoTInfoOverallUrl = GetWoTInfoOverallUrl(player),
                    WoTInfoRecentUrl = GetWoTInfoRecentUrl(player),
                    WoTInfoHistoryUrl = GetWoTInfoHistoryUrl(player),

                    WoTConsoleRuOverallUrl = GetWoTConsoleRuOverallUrl(player),
                    WotConsoleRuClanUrl = GetWotConsoleRuClanUrl(clan),

                    WoTStarsOverallUrl = GetWoTStarsOverallUrl(player),
                    WoTStarsClanUrl = GetWoTStarsClanUrl(clan),
                };

                return View(model);
            }
            catch (Exception ex)
            {
                Log.Error($"Recent({clanName}, {playerId})", ex);
                return Redirect(url);
            }
        }

        private static string GetDefaultRecentUrl(Player player)
        {
            switch (GlobalHelper.DefaultPlayerDetails)
            {
                case PlayerDataOrigin.WotInfo:
                    return GetWoTInfoRecentUrl(player);
                case PlayerDataOrigin.WotStatConsole:
                    return GetWoTStatConsoleRecentUrl(player);
                default:
                    // As WoTInfo appears to be more stable...
                    return GetWoTInfoRecentUrl(player);
            }
        }

        private static string GetWoTStatConsoleRecentUrl(Player player)
        {
            return GetWoTStatConsoleOverallUrl(player) + "#diagram";
        }

        private static string GetWoTStatConsoleHistoryUrl(Player player)
        {
            return GetWoTStatConsoleOverallUrl(player) + "#history";
        }

        private static string GetWoTInfoRecentUrl(Player player)
        {
            // http://wotinfo.net/en/recent?playerid=1076862894&server=PS4

            var lang = GlobalHelper.Language;
            string externalLang;
            switch (lang)
            {
                case "de":
                    externalLang = "de";
                    break;
                case "ru":
                    externalLang = "ru";
                    break;
                default:
                    externalLang = "en";
                    break;
            }

            var p = player.Platform == Platform.PS ? "PS4" : "XBOX";

            var url = $"http://wotinfo.net/{externalLang}/recent?playerid={player.Id}&server={p}";
            return url;
        }
    }
}