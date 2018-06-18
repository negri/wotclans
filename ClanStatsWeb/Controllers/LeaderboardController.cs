using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Negri.Wot.Models;
using Negri.Wot.Properties;
using Negri.Wot.WgApi;
using Newtonsoft.Json;

namespace Negri.Wot.Site.Controllers
{
    public class LeaderboardController : Controller
    {
        [OutputCache(CacheProfile = "Normal")]
        public ActionResult Aces()
        {
            var model = GetTankLeaders(order: 1);
            return View(model);
        }

        [OutputCache(CacheProfile = "Normal")]
        public ActionResult Leaderboard()
        {
            var model = GetTankLeaders(upToOrder: 25, page: 0, limit: 25);
            return View(model);
        }

        // Para retorno na grade
        public ActionResult LeaderboardGrid()
        {
            try
            {
                int draw = int.Parse(Request["draw"]);

                Nation? nation = null;
                var s = Request["columns[0][search][value]"];
                if (!string.IsNullOrWhiteSpace(s))
                {
                    nation = (Nation)Enum.Parse(typeof(Nation), s, true);
                }

                bool? isPremium = null;
                TankType? tankType = null;
                s = Request["columns[1][search][value]"];
                if (!string.IsNullOrWhiteSpace(s))
                {
                    if (s == "Premium")
                    {
                        isPremium = true;
                    }
                    else if (s == "Regular")
                    {
                        isPremium = false;
                    }
                    else
                    {
                        tankType = (TankType)Enum.Parse(typeof(TankType), s, true);
                    }
                }

                int? tier = null;
                s = Request["columns[2][search][value]"];
                if (!string.IsNullOrWhiteSpace(s))
                {
                    tier = int.Parse(s);
                }

                var globalSearch = Request["search[value]"];

                var model = GetTankLeaders(upToOrder: 25, nation: nation, tankType: tankType, isPremium: isPremium,
                    tier: tier, globalSearch: globalSearch);

                var data = model.Leaders.AsEnumerable();

                for (int i = 0; i <= 12; ++i)
                {
                    s = Request[$"order[{i}][column]"];
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        int c = int.Parse(s);
                        string dir = Request[$"order[{i}][dir]"];
                        if (!string.IsNullOrWhiteSpace(dir))
                        {
                            switch (c)
                            {
                                case 0:
                                    data = data.OrderBy(t => t.Nation);
                                    break;
                                case 1:
                                    data = data.OrderBy(t => t.Type);
                                    break;
                                case 2:
                                    data = data.OrderBy(t => t.Tier);
                                    break;
                                case 3:
                                    data = data.OrderBy(t => t.Name.RemoveDiacritics().ToLowerInvariant());
                                    break;
                                case 4:
                                    data = data.OrderBy(t => t.ClanTag.ToLowerInvariant().Replace("_", "")
                                        .Replace("-", ""));
                                    break;
                                case 5:
                                    data = data.OrderBy(t => t.GamerTag.RemoveDiacritics().ToLowerInvariant());
                                    break;
                                case 6:
                                    data = data.OrderBy(t => t.Order);
                                    break;
                                case 7:
                                    data = data.OrderBy(t => t.Battles);
                                    break;
                                case 8:
                                    data = data.OrderBy(t => t.DirectDamage);
                                    break;
                                case 9:
                                    data = data.OrderBy(t => t.DamageAssisted);
                                    break;
                                case 10:
                                    data = data.OrderBy(t => t.TotalDamage);
                                    break;
                                case 11:
                                    data = data.OrderBy(t => t.Kills);
                                    break;
                                case 12:
                                    data = data.OrderBy(t => t.MaxKills);
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

                int start = int.Parse(Request["start"] ?? "0");
                int lenght = int.Parse(Request["length"] ?? "25");
                data = data.Skip(start).Take(lenght).ToArray();

                var o = new
                {
                    draw,
                    recordsTotal = model.TotalLeaders,
                    recordsFiltered = model.Returned,
                    data = data.Select(t => new
                    {
                        DT_RowId = $"id_{t.TankId}_{t.PlayerId}",
                        DT_RowClass = "text-nowrap",
                        Nation = $"{(int)t.Nation}|{Resources.ResourceManager.GetString(t.Nation.ToString())}",
                        Type = $"{(int)t.Type}|{Resources.ResourceManager.GetString(t.Type.ToString())}",
                        Tier = t.Tier.ToRomanNumeral(),
                        Tank = $"{t.Name}|{t.Tag}|{t.FullName}|{t.PremiumClass}|{t.Nation}|{t.TankId}",
                        ClanTag = $"{t.ClanTag}|{t.ClanFlag ?? string.Empty}",
                        Commander = $"{t.GamerTag}|{t.PlayerId}|{t.ClanTag}",
                        Rank = t.Order,
                        Battles = t.Battles.ToString("N0"),
                        DirectDamage = t.DirectDamage.ToString("N0"),
                        DamageAssisted = t.DamageAssisted.ToString("N0"),
                        TotalDamage = t.TotalDamage.ToString("N0"),
                        Kills = t.Kills.ToString("N2"),
                        t.MaxKills
                    }).ToArray()
                };

                var json = JsonConvert.SerializeObject(o, Formatting.Indented);
                return Content(json, "application/json", System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                var o = new
                {
                    error = ex.ToString()
                };
                var json = JsonConvert.SerializeObject(o, Formatting.Indented);
                return Content(json, "application/json", System.Text.Encoding.UTF8);
            }
        }

        private TankLeaders GetTankLeaders(DateTime? date = null,
            int? limit = null, int? page = null,
            long? tankId = null, long? playerId = null, long? clanId = null,
            int? order = null, int? upToOrder = null,
            int? tier = null, Nation? nation = null, TankType? tankType = null, bool? isPremium = null,
            string gamerTag = null, string tankName = null, string clanTag = null, string clanFlag = null, string globalSearch = null)
        {
            var getter = HttpRuntime.Cache.Get("FileGetter", GlobalHelper.CacheMinutes,
                () => new FileGetter(GlobalHelper.DataFolder));

            var allLeaders = getter.GetTankLeaders(date).ToArray();

            var filtered = allLeaders.AsEnumerable();
            if (order.HasValue)
            {
                filtered = filtered.Where(l => l.Order == order.Value);
            }
            if (upToOrder.HasValue)
            {
                filtered = filtered.Where(l => l.Order <= upToOrder.Value);
            }
            if (tankId.HasValue)
            {
                filtered = filtered.Where(l => l.TankId == tankId.Value);
            }
            if (tier.HasValue)
            {
                filtered = filtered.Where(l => l.Tier == tier.Value);
            }
            if (nation.HasValue)
            {
                filtered = filtered.Where(l => l.Nation == nation.Value);
            }
            if (tankType.HasValue)
            {
                filtered = filtered.Where(l => l.Type == tankType.Value);
            }
            if (isPremium.HasValue)
            {
                filtered = filtered.Where(l => l.IsPremium == isPremium.Value);
            }
            if (playerId.HasValue)
            {
                filtered = filtered.Where(l => l.PlayerId == playerId.Value);
            }
            if (clanId.HasValue)
            {
                filtered = filtered.Where(l => l.ClanId == clanId.Value);
            }
            if (!string.IsNullOrWhiteSpace(gamerTag))
            {
                filtered = filtered
                    .Where(l => gamerTag.EqualsCiAi(l.GamerTag));
            }
            if (!string.IsNullOrWhiteSpace(tankName))
            {
                filtered = filtered
                    .Where(l => tankName.EqualsCiAi(l.Name));
            }
            if (!string.IsNullOrWhiteSpace(clanTag))
            {
                filtered = filtered
                    .Where(l => clanTag.EqualsCiAi(l.ClanTag));
            }
            if (!string.IsNullOrWhiteSpace(clanFlag))
            {
                filtered = filtered
                    .Where(l => clanFlag.EqualsCiAi(l.ClanFlag));
            }
            if (!string.IsNullOrWhiteSpace(globalSearch))
            {
                filtered = filtered.Where(l => l.IsGlobalMatch(globalSearch));
            }

            if (limit != null)
            {
                if (limit < 10)
                {
                    limit = 10;
                }
                if (limit > 5000)
                {
                    limit = 5000;
                }
            }
            if (page < 1)
            {
                page = 1;
            }
            if ((page != null) && (limit == null))
            {
                limit = 10;
            }

            if ((page == null) && (limit != null))
            {
                filtered = filtered.Take(limit.Value);
            }
            else if ((page != null) && (limit != null))
            {
                if (page < 1)
                {
                    page = 1;
                }
                filtered = filtered.Skip((page.Value - 1) * limit.Value).Take(limit.Value);
            }



            var filteredArray = filtered.ToArray();

            return new TankLeaders
            {
                TotalLeaders = allLeaders.Length,
                Leaders = filteredArray,
            };
        }

        public ActionResult ApiLeaders(DateTime? date = null,
            int? limit = null, int? page = null,
            long? tankId = null, long? playerId = null, long? clanId = null,
            int? order = null, int? upToOrder = null,
            int? tier = null, Nation? nation = null, TankType? tankType = null, bool? isPremium = null,
            string gamerTag = null, string tankName = null, string clanTag = null, string clanFlag = null)
        {
            var result = GetTankLeaders(date, limit, page, tankId, playerId, clanId, order, upToOrder, tier, nation,
                tankType, isPremium, gamerTag, tankName, clanTag, clanFlag);

            var json = JsonConvert.SerializeObject(result, Formatting.Indented);
            return Content(json, "application/json", System.Text.Encoding.UTF8);
        }



    }
}