using System.Web.Mvc;
using System.Web.Routing;

namespace Negri.Wot
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
                name: "About",
                url: "About",
                defaults: new { controller = "Home", action = "About" });

            routes.MapRoute(
                name: "DiscordBot",
                url: "DiscordBot",
                defaults: new { controller = "Home", action = "DiscordBot" });

            routes.MapRoute(
                name: "Donate",
                url: "Donate",
                defaults: new { controller = "Home", action = "Donate" });

            routes.MapRoute(
                name: "PlayerOverall",
                url: "Clan/{clanName}/Commanders/{playerId}/All",
                defaults: new { controller = "Players", action = "Overall" });

            routes.MapRoute(
                name: "PlayerRecent",
                url: "Clan/{clanName}/Commanders/{playerId}/Recent",
                defaults: new { controller = "Players", action = "Recent" });

            routes.MapRoute(
                name: "Commanders",
                url: "Clan/{clanName}/Commanders",
                defaults: new { controller = "Home", action = "Commanders"});
            
            routes.MapRoute(
                name: "Clan",
                url: "Clan/{clanName}",
                defaults: new { controller = "Home", action = "Clan" });

            routes.MapRoute(
                name: "Tournament",
                url: "Tournament/{tournament}",
                defaults: new { controller = "Home", action = "Tournament" });

            routes.MapRoute(
                name: "ClanRoot",
                url: "Clan",
                defaults: new { controller = "Home", action = "ClanRoot" });            

            // To not duplicate code, keep this API where it is
            routes.MapRoute(
                name: "ApiLeaderboard",
                url: "api/leaderboard/{date}",
                defaults: new { controller = "Leaderboard", action = "ApiLeaders", date = UrlParameter.Optional });
            
            routes.MapRoute(
                name: "TanksMoe",
                url: "Tanks/MoE/{date}",
                defaults: new { controller = "Tanks", action = "Moe", date = UrlParameter.Optional });

            routes.MapRoute(
                name: "TanksWn8Excel",
                url: "Tanks/WN8/Excel/{date}",
                defaults: new { controller = "Tanks", action = "WN8Excel", date = UrlParameter.Optional });

            routes.MapRoute(
                name: "TanksWn8",
                url: "Tanks/WN8/{date}",
                defaults: new { controller = "Tanks", action = "WN8", date = UrlParameter.Optional });

            routes.MapRoute(
                name: "TanksStats",
                url: "Tanks/{tankId}",
                defaults: new { controller = "Tanks", action = "Stats" });

            routes.MapRoute(
                name: "LeaderboardAces",
                url: "Leaderboard/Aces",
                defaults: new { controller = "Leaderboard", action = "Aces" });

            routes.MapRoute(
                name: "TanksAces",
                url: "Tanks/Aces",
                defaults: new { controller = "Tanks", action = "Aces" });

            routes.MapRoute(
                name: "LeaderboardGrid",
                url: "Leaderboard/Grid",
                defaults: new { controller = "Leaderboard", action = "LeaderboardGrid" });

            routes.MapRoute(
                name: "LeaderboardAll",
                url: "Leaderboard/All",
                defaults: new { controller = "Leaderboard", action = "Leaderboard" });

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );



        }
    }
}
