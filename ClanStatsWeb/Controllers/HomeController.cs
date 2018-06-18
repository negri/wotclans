using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;
using Negri.Wot.Models;
using Negri.Wot.Site;
using Newtonsoft.Json;

namespace Negri.Wot.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index(string countryFilter = null, bool invertCountryFilter = false, int minActiveSize = 7,
            int maxActiveSize = 200,
            int minWn8T15 = 900, string tournament = null)
        {
            var getter = HttpRuntime.Cache.Get("FileGetter", GlobalHelper.CacheMinutes,
                () => new FileGetter(GlobalHelper.DataFolder));
            var clans = getter.GetAllRecent().OrderByDescending(c => c.Top15Wn8).ToArray();
            var clansPage = new ClansPage(clans, countryFilter, invertCountryFilter, minActiveSize, maxActiveSize,
                minWn8T15);

            return View(clansPage);
        }

        public ActionResult Tournament(string tournament, string countryFilter = null, int minActiveSize = 0,
            int maxActiveSize = 200,
            int minWn8T15 = 0)
        {
            var getter = HttpRuntime.Cache.Get("FileGetter", GlobalHelper.CacheMinutes,
                () => new FileGetter(GlobalHelper.DataFolder));
            var clans = getter.GetAllRecent().OrderByDescending(c => c.Top15Wn8).ToArray();

            var t = Wot.Tournament.Read(GlobalHelper.DataFolder, tournament);

            var tournamentClans = new HashSet<string>(t.Clans);
            clans = clans.Where(c => tournamentClans.Contains(c.ClanTag)).ToArray();

            var clansPage = new ClansPage(clans, countryFilter, false, minActiveSize, maxActiveSize, minWn8T15)
            {
                Tournament = t
            };

            return View(clansPage);
        }

        [OutputCache(CacheProfile = "Normal")]
        public ActionResult ClanRoot()
        {
            return RedirectToActionPermanent("Index");
        }

        [OutputCache(CacheProfile = "Normal")]
        public ActionResult About()
        {
            ViewBag.Message = "About this site";

            return View();
        }

        [OutputCache(CacheProfile = "Normal")]
        public ActionResult DiscordBot()
        {
            ViewBag.Message = "The Discord Bot";
            return View();
        }

        [OutputCache(CacheProfile = "Normal")]
        public ActionResult Donate()
        {
            ViewBag.Message = "Donate!";

            return View();
        }

        public ActionResult Clan(string clanName, ShowPlayersMode showPlayersMode = ShowPlayersMode.AllActive)
        {
            // Visualizações não mais usadas
            if ((showPlayersMode != ShowPlayersMode.AllActive) && (showPlayersMode != ShowPlayersMode.All))
            {
                return RedirectPermanent($"~/Clan/{clanName}");
            }

            var getter = HttpRuntime.Cache.Get("FileGetter", GlobalHelper.CacheMinutes,
                () => new FileGetter(GlobalHelper.DataFolder));

            var clan = getter.GetClan(clanName);

            if (clan == null)
            {
                var newName = getter.GetRenamedClan(clanName);
                if (string.IsNullOrWhiteSpace(newName))
                {
                    // O Clan não existe
                    return HttpNotFound($"The clan with tag {clanName} could not be found.");
                }

                // Clã trocou de nome
                return RedirectPermanent($"~/Clan/{newName}");
            }

            var clanPage = new ClanPage
            {
                Clan = clan,
                ShowPlayersMode = showPlayersMode,
                Leaders = getter.GetTankLeaders(clan.Date).Where(l => l.ClanTag == clan.ClanTag).ToArray()
            };

            return View(clanPage);
        }

        public ActionResult Commanders(string clanName,
            ShowPlayersMode showPlayersMode = ShowPlayersMode.AllActive)
        {
            var getter = HttpRuntime.Cache.Get("FileGetter", GlobalHelper.CacheMinutes,
                () => new FileGetter(GlobalHelper.DataFolder));

            var clan = getter.GetClan(clanName);

            if (clan == null)
            {
                var newName = getter.GetRenamedClan(clanName);
                if (string.IsNullOrWhiteSpace(newName))
                {
                    return HttpNotFound($"The clan with tag {clanName} could not be found.");
                }

                return RedirectPermanent($"~/Clan/{newName}");
            }


            var clanPage = new ClanPage
            {
                Clan = clan,
                ShowPlayersMode = showPlayersMode
            };

            return View(clanPage);
        }


        public ActionResult ApiIndex(string overrideAccept = null)
        {
            var getter = HttpRuntime.Cache.Get("FileGetter", GlobalHelper.CacheMinutes,
                () => new FileGetter(GlobalHelper.DataFolder));

            var clans = getter.GetAllRecent(true).OrderByDescending(c => c.Top15Wn8).ToArray();
            var clansPage = new ClansPage(clans);

            if (Request.AcceptTypes != null && Request.AcceptTypes.Contains("application/text") ||
                overrideAccept == "application/text")
            {
                var sb = new StringBuilder();
                sb.AppendLine(
                    "ClanTag;Name;Country;Moment;Actives;ActiveBattles;ActiveWinRate;WN8a;WN8t15;WN8t7;Members;Battles;WinRate;WN8;IsOldData;IsObsolete");
                foreach (var c in clans)
                {
                    sb.AppendFormat(CultureInfo.CurrentCulture,
                        "{0};{1};{2};{3:o};{4};{5};{6:r};{7:r};{8:r};{9:r};{10};{11};{12:r};{13:r};{14};{15}",
                        c.ClanTag, c.Name, c.Country, c.Moment, c.Active, c.ActiveBattles, c.ActiveWinRate,
                        c.ActiveWn8, c.Top15Wn8, c.Top7Wn8, c.Count, c.TotalBattles, c.TotalWinRate, c.TotalWn8,
                        c.IsOldData, c.IsObsolete);
                    sb.AppendLine();
                }

                return Content(sb.ToString(), "text/plain", Encoding.UTF8);
            }

            var json = JsonConvert.SerializeObject(clansPage, Formatting.Indented);
            return Content(json, "application/json", Encoding.UTF8);
        }


    }
}