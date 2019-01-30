using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using log4net;
using log4net.Config;
using System.Web.Http;

namespace Negri.Wot.Site
{
    /// <summary>
    /// A aplicação
    /// </summary>
    public class MvcApplication : HttpApplication
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MvcApplication));

        protected void Application_Start()
        {
            try
            {
                string appPath = HttpContext.Current.Server.MapPath("~");
                string configFile = Path.Combine(appPath, "log4net.config");
                XmlConfigurator.ConfigureAndWatch(new FileInfo(configFile));
            }
            catch (Exception ex)
            {
                Trace.Write(ex);
            }

            GlobalConfiguration.Configure(WebApiConfig.Register);

            GlobalHelper.DataFolder = ConfigurationManager.AppSettings["ClanResultsFolder"];
            GlobalHelper.Platform = (Platform) Enum.Parse(typeof (Platform), ConfigurationManager.AppSettings["Plataform"]);
            GlobalHelper.CacheMinutes = int.Parse(ConfigurationManager.AppSettings["CacheMinutes"] ?? "0");
            GlobalHelper.DefaultPlayerDetails = (PlayerDataOrigin)Enum.Parse(typeof(PlayerDataOrigin), 
                ConfigurationManager.AppSettings["DefaultPlayerDetails"] ?? PlayerDataOrigin.WotInfo.ToString());
            GlobalHelper.UseExternalPlayerPage = bool.Parse(ConfigurationManager.AppSettings["UseExternalPlayerPage"] ?? "false");

            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            Log.InfoFormat("App started for {0}, on data folder {1}, using {2}min of cache time.",
                GlobalHelper.Platform, GlobalHelper.DataFolder, GlobalHelper.CacheMinutes);
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            Exception ex = Server.GetLastError();         
            Log.Error(ex);
        }



    }
}