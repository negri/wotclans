using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;
using log4net;
using Negri.Wot.Models;
using Negri.Wot.Site;
using Newtonsoft.Json;

namespace Negri.Wot.Controllers
{
    public class HomeController : Controller
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(HomeController));

        public ActionResult Index()
        {
            var getter = HttpRuntime.Cache.Get("FileGetter", GlobalHelper.CacheMinutes, () => new FileGetter(GlobalHelper.DataFolder));
            var clans = getter.GetAllRecent().ToArray();
            var clansPage = new ClansPage(clans);

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


        public ActionResult ApiIndex(string overrideAccept = null, string countryFilter = null, int minActiveSize = 0, int maxActiveSize = 200,
            int minWn8T15 = 0, int maxWn8T15 = 8000, string clanFilter = null)
        {
            var getter = HttpRuntime.Cache.Get("FileGetter", GlobalHelper.CacheMinutes, () => new FileGetter(GlobalHelper.DataFolder));
            var clans = getter.GetAllRecent(true).ToArray();
            var apiClansReturn = new ApiClansReturn(clans, countryFilter, minActiveSize, maxActiveSize, minWn8T15, maxWn8T15, clanFilter);

            if (Request.AcceptTypes != null && Request.AcceptTypes.Contains("application/text") || overrideAccept == "application/text")
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

            var json = JsonConvert.SerializeObject(apiClansReturn, Formatting.Indented);
            return Content(json, "application/json", Encoding.UTF8);
        }

        // To grid dynamic return
        public ActionResult ClansGrid()
        {
            try
            {
                int draw = 0;
                var s = Request["draw"];
                if (!string.IsNullOrWhiteSpace(s))
                {
                    draw = int.Parse(s);
                }
                
                // country filter and clan filter
                string countryFilter = null;
                string clanFilter = null;
                s = Request["columns[1][search][value]"];
                if (!string.IsNullOrWhiteSpace(s))
                {
                    var a = s.Split(';');
                    if (a.Length >= 1)
                    {
                        clanFilter = a[0].Trim().ToUpperInvariant();
                    }

                    if (a.Length >= 2)
                    {
                        countryFilter = a[1].Trim().ToUpperInvariant();
                    }
                }

                // Activity Filter
                var minActiveSize = 7;
                var maxActiveSize = 200;
                s = Request["columns[3][search][value]"];
                if (!string.IsNullOrWhiteSpace(s))
                {
                    var a = s.Split(';');
                    if (a.Length >= 1)
                    {
                        minActiveSize = int.Parse(a[0].Trim());
                    }

                    if (a.Length >= 2)
                    {
                        maxActiveSize = int.Parse(a[1].Trim());
                    }
                }

                var getter = HttpRuntime.Cache.Get("FileGetter", GlobalHelper.CacheMinutes, () => new FileGetter(GlobalHelper.DataFolder));
                var clans = getter.GetAllRecent(true).ToArray();
                var apiClansReturn = new ApiClansReturn(clans, countryFilter, minActiveSize, maxActiveSize, 0, int.MaxValue, clanFilter);

                var data = apiClansReturn.Clans.ToArray().AsEnumerable();

                for (var i = 0; i <= 12; ++i)
                {
                    s = Request[$"order[{i}][column]"];
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        var c = int.Parse(s);
                        var dir = Request[$"order[{i}][dir]"];
                        if (!string.IsNullOrWhiteSpace(dir))
                        {
                            switch (c)
                            {
                                case 0:
                                    data = data.OrderBy(t => t.Rank);
                                    break;
                                case 1:
                                    data = data.OrderBy(t => t.ClanTag.TrimStart('_', '-').ToUpperInvariant());
                                    break;
                                case 2:
                                    data = data.OrderBy(t => t.CompositionPs);
                                    break;
                                case 3:
                                    data = data.OrderBy(t => t.Active);
                                    break;
                                case 4:
                                    data = data.OrderBy(t => t.ActiveBattles);
                                    break;
                                case 5:
                                    data = data.OrderBy(t => t.ActiveWinRate);
                                    break;
                                case 6:
                                    data = data.OrderBy(t => t.ActiveWn8);
                                    break;
                                case 7:
                                    data = data.OrderBy(t => t.Top15Wn8);
                                    break;
                                case 8:
                                    data = data.OrderBy(t => t.Top7Wn8);
                                    break;
                                case 9:
                                    data = data.OrderBy(t => t.Count);
                                    break;
                                case 10:
                                    data = data.OrderBy(t => t.TotalBattles);
                                    break;
                                case 11:
                                    data = data.OrderBy(t => t.TotalWinRate);
                                    break;
                                case 12:
                                    data = data.OrderBy(t => t.TotalWn8);
                                    break;
                            }

                            if (dir == "desc")
                            {
                                data = data.Reverse();
                            }

                            data = data.ToArray().AsEnumerable();
                        }
                    }
                }

                var start = int.Parse(Request["start"] ?? "0");
                var length = int.Parse(Request["length"] ?? "25");

                var enumerable = data as Clan[] ?? data.ToArray();
                var filtered = enumerable.ToArray();

                var paged = filtered.Skip(start).Take(length).ToArray();

                var o = new
                {
                    draw,
                    recordsTotal = apiClansReturn.Count,
                    recordsFiltered = filtered.Length,
                    data = paged.Select(c => new
                    {
                        DT_RowId = $"id_{c.ClanId}",
                        DT_RowClass = "text-nowrap",

                        c.Rank,
                        ClanTag = $"{c.ClanTag};{c.Country ?? string.Empty};{c.IsOldData};{c.Name.Replace(";","|ç|")}",
                        Composition = $"{c.CompositionPs*100.0:N0};{c.CountPs};{c.CountXbox}",
                        Active = c.Active.ToString("N0"),
                        ActiveBattles = $"{c.ActiveBattles:N0};{c.Moment:O}",
                        ActiveWinRate = c.ActiveWinRate.ToString("P1"),
                        ActiveWn8 = $"{c.ActiveWn8:N0};{c.ActiveWn8.ToLabelClass()};{c.ActiveWn8.ToRatingString()};{c.ActiveWn8.ToWebColor()}",
                        Top15Wn8 = $"{c.Top15Wn8:N0};{c.Top15Wn8.ToLabelClass()};{c.Top15Wn8.ToRatingString()};{c.Top15Wn8.ToWebColor()}",
                        Top7Wn8 = $"{c.Top7Wn8:N0};{c.Top7Wn8.ToLabelClass()};{c.Top7Wn8.ToRatingString()};{c.Top7Wn8.ToWebColor()}",
                        Count = c.Count.ToString("N0"),
                        TotalBattles = c.TotalBattles.ToString("N0"),
                        TotalWinRate = c.TotalWinRate.ToString("P1"),
                        TotalWn8 = $"{c.TotalWn8:N0};{c.TotalWn8.ToLabelClass()};{c.TotalWn8.ToRatingString()};{c.TotalWn8.ToWebColor()}"
                    }).ToArray()
                };

                var json = JsonConvert.SerializeObject(o, Formatting.Indented);
                return Content(json, "application/json", Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Log.Error("Grid Error", ex);
                var o = new
                {
                    error = ex.ToString()
                };
                var json = JsonConvert.SerializeObject(o, Formatting.Indented);
                return Content(json, "application/json", Encoding.UTF8);
            }
        }
    }
}