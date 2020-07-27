using System;
using System.Configuration;
using System.Threading.Tasks;
using CliFx;
using log4net;
using Microsoft.Extensions.DependencyInjection;
using Negri.Wot.Sql;

namespace Negri.Wot
{
    class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Program));

        public static async Task<int> Main()
        {
            Log.Debug("Starting up...");

            var services = new ServiceCollection();

            var webCacheAge = TimeSpan.FromMinutes(1);
            var cacheDirectory = ConfigurationManager.AppSettings["CacheDirectory"];
            var wargamingApplicationId = ConfigurationManager.AppSettings["WgAppId"];
            var wotClansAdminApiKey = ConfigurationManager.AppSettings["ApiAdminKey"];

            services.AddTransient(p => new Fetcher(cacheDirectory)
            {
                WebCacheAge = webCacheAge,
                WebFetchInterval = TimeSpan.FromSeconds(1),
                WargamingApplicationId = wargamingApplicationId,
                WotClansAdminApiKey = wotClansAdminApiKey
            });

            var ftpFolder = ConfigurationManager.AppSettings["FtpFolder"];
            var ftpUser = ConfigurationManager.AppSettings["FtpUser"];
            var ftpPassword = ConfigurationManager.AppSettings["FtpPassword"];

            services.AddTransient(p => new FtpPutter(ftpFolder, ftpUser, ftpPassword));

            var connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;

            services.AddTransient(p => new DbProvider(connectionString));
            services.AddTransient(p => new DbRecorder(connectionString));

            var resultDirectory = ConfigurationManager.AppSettings["ResultDirectory"];

            services.AddTransient<ImportXvmCommand>(p =>
                new ImportXvmCommand(
                    p.GetService<Fetcher>(),
                    p.GetService<FtpPutter>(),
                    p.GetService<DbProvider>(),
                    p.GetService<DbRecorder>(),
                    resultDirectory
                ));

            var serviceProvider = services.BuildServiceProvider();

            return await new CliApplicationBuilder()
                .AddCommandsFromThisAssembly()
                .UseTypeActivator(serviceProvider.GetService)
                .Build()
                .RunAsync();
        }
    }
}
