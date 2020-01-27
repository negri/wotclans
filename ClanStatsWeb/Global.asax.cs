using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
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
            GlobalHelper.Platform =
                (Platform) Enum.Parse(typeof(Platform), ConfigurationManager.AppSettings["Plataform"]);
            GlobalHelper.CacheMinutes = int.Parse(ConfigurationManager.AppSettings["CacheMinutes"] ?? "0");
            GlobalHelper.DefaultPlayerDetails = (PlayerDataOrigin) Enum.Parse(typeof(PlayerDataOrigin),
                ConfigurationManager.AppSettings["DefaultPlayerDetails"] ?? PlayerDataOrigin.WotInfo.ToString());
            GlobalHelper.UseExternalPlayerPage =
                bool.Parse(ConfigurationManager.AppSettings["UseExternalPlayerPage"] ?? "false");

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

        /// <summary>
        /// Languages that I have translations
        /// </summary>
        private static readonly HashSet<string> ExistingTranslations = new HashSet<string>
            {"en", "de", "es", "fr", "pl", "pt", "ru"};

        /// <summary>
        /// Being used to improve on ASP.Net MVC automated language and culture detection
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void Application_AcquireRequestState(object sender, EventArgs e)
        {
            try
            {
                HandleLanguageAndCulture();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        private static void HandleLanguageAndCulture()
        {
            Log.Debug($"Start Culture: {Thread.CurrentThread.CurrentCulture.Name}; UI: {Thread.CurrentThread.CurrentUICulture.Name}");

            if (HttpContext.Current == null)
            {
                Log.Debug("No HttpContext.Current");
                return;
            }

            var userLanguages = HttpContext.Current.Request.UserLanguages;
            if (userLanguages == null || userLanguages.Length == 0)
            {
                Log.Debug("No User Language on request");
                return;
            }

            foreach (var userLanguage in userLanguages)
            {
                if (string.IsNullOrWhiteSpace(userLanguage))
                {
                    continue;
                }

                if (userLanguage.Length < 2)
                {
                    continue;
                }

                var lang = userLanguage.Substring(0, 2).ToLowerInvariant();

                if (ExistingTranslations.Contains(lang))
                {
                    if (lang == Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName)
                    {
                        // It's a match! But maybe automatic detection already worked...
                        return;
                    }

                    var currentLanguageRegion = Thread.CurrentThread.CurrentCulture.Name;
                    var posSeparator = currentLanguageRegion.IndexOf('-');
                    var region = string.Empty;
                    if (posSeparator > 0)
                    {
                        region = currentLanguageRegion.Substring(posSeparator);
                    }
                    var newLanguageRegion = lang;
                    if (!string.IsNullOrWhiteSpace(region))
                    {
                        newLanguageRegion += region;
                    }

                    var newCi = CreateCulture(newLanguageRegion);
                    if (newCi == null)
                    {
                        // The combination of language and region didn't worked... just the language should work
                        newCi = CreateCulture(lang);
                    }

                    if (newCi == null)
                    {
                        Log.Warn($"Can't create culture for language and region... fall back to next...");
                        continue;
                    }

                    Thread.CurrentThread.CurrentCulture = newCi;
                    Thread.CurrentThread.CurrentUICulture = newCi;

                    Log.Debug($"User language {userLanguage} was mapped to {newCi.Name}");

                    return;
                }
            }
        }

        private static CultureInfo CreateCulture(string culture)
        {
            try
            {
                return CultureInfo.GetCultureInfo(culture);
            }
            catch (CultureNotFoundException ex)
            {
                Log.Error($"Creating Culture '{culture}'", ex);
                return null;
            }
            catch (Exception ex)
            {
                Log.Error($"Creating Culture '{culture}'", ex);
                return null;
            }
        }
    

    }
}