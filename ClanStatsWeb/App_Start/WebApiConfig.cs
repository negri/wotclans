using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Formatting;
using System.Web.Http;

namespace Negri.Wot.Site
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            config.Formatters.Add(new BsonMediaTypeFormatter());

            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "CleanDataFolders",
                routeTemplate: "api/admin/CleanDataFolders",
                defaults: new {controller = "GeneralApi", action = "CleanDataFolders"}
            );

            config.Routes.MapHttpRoute(
                name: "DeleteClan",
                routeTemplate: "api/admin/DeleteClan",
                defaults: new { controller = "GeneralApi", action = "DeleteClan" }
            );

            config.Routes.MapHttpRoute(
                name: "RenameClan",
                routeTemplate: "api/admin/RenameClan",
                defaults: new { controller = "GeneralApi", action = "RenameClan" }
            );

            config.Routes.MapHttpRoute(
                name: "PutData",
                routeTemplate: "api/admin/Data",
                defaults: new { controller = "GeneralApi", action = "PutData" }
            );

            config.Routes.MapHttpRoute(
                name: "ApiStatus",
                routeTemplate: "api/status",
                defaults: new { controller = "GeneralApi", action = "GetStatus" }
            );

            config.Routes.MapHttpRoute(
                name: "ApiIndex",
                routeTemplate: "api/clan",
                defaults: new { controller = "GeneralApi", action = "GetClans" });

            config.Routes.MapHttpRoute(
                name: "ApiClan",
                routeTemplate: "api/clan/{clanTag}",
                defaults: new { controller = "GeneralApi", action = "GetClan" });

            config.Routes.MapHttpRoute(
                name: "ApiTanksMoe",
                routeTemplate: "api/tanks/moe/{date}",
                defaults: new { controller = "GeneralApi", action = "GetMoE", date = RouteParameter.Optional });

            config.Routes.MapHttpRoute(
                name: "ApiTanksWn8",
                routeTemplate: "api/tanks/wn8/{date}",
                defaults: new { controller = "GeneralApi", action = "GetWN8", date = RouteParameter.Optional });

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );
        }
    }
}
