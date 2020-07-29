using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using CliFx;
using log4net;
using log4net.Config;
using Microsoft.Extensions.DependencyInjection;
using Negri.Wot.Commands;
using Negri.Wot.Sql;

namespace Negri.Wot
{
    class Program
    {

        
        public static int Main(string[] args)
        {
            if (File.Exists("log4net.config"))
            {
                XmlConfigurator.Configure(new FileInfo("log4net.config"));
            }

            // To accept expired certificates
            ServicePointManager.ServerCertificateValidationCallback = ServerCertificateValidationCallback;

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

            services.AddTransient(p => new Putter(wotClansAdminApiKey));

            var ftpFolder = ConfigurationManager.AppSettings["FtpFolder"];
            var ftpUser = ConfigurationManager.AppSettings["FtpUser"];
            var ftpPassword = ConfigurationManager.AppSettings["FtpPassword"];

            services.AddTransient(p => new FtpPutter(ftpFolder, ftpUser, ftpPassword));

            var connectionString = ConfigurationManager.ConnectionStrings["Main"].ConnectionString;

            services.AddTransient(p => new DbProvider(connectionString));
            services.AddTransient(p => new DbRecorder(connectionString));

            var resultDirectory = ConfigurationManager.AppSettings["ResultDirectory"];

            services.AddTransient(p =>
                new ImportXvm(
                    p.GetService<Fetcher>(),
                    p.GetService<FtpPutter>(),
                    p.GetService<DbProvider>(),
                    p.GetService<DbRecorder>(),
                    resultDirectory
                ));

            services.AddTransient(p =>
                new CalculateStats(
                    p.GetService<Fetcher>(),
                    p.GetService<FtpPutter>(),
                    p.GetService<DbProvider>(),
                    p.GetService<DbRecorder>(),
                    resultDirectory
                ));

            services.AddTransient(p =>
                new GetClans(
                    p.GetService<Fetcher>(),
                    p.GetService<FtpPutter>(),
                    p.GetService<Putter>(),
                    p.GetService<DbProvider>(),
                    p.GetService<DbRecorder>(),
                    resultDirectory
                ));

            services.AddTransient(p =>
                new GetPlayers(
                    p.GetService<Fetcher>(),
                    p.GetService<Putter>(),
                    p.GetService<DbProvider>(),
                    p.GetService<DbRecorder>()
                ));

            var serviceProvider = services.BuildServiceProvider();

            var cab= new CliApplicationBuilder()
                .AddCommandsFromThisAssembly()
                .UseTypeActivator(serviceProvider.GetService)
                .Build()
                .RunAsync();

            return cab.Result;
        }

        private static bool ServerCertificateValidationCallback(object s, X509Certificate certificate, X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None) return true;

            var expDateString = certificate.GetExpirationDateString();
            var expDate = DateTime.Parse(expDateString, CultureInfo.CurrentCulture);
            if (expDate < DateTime.Now)
                // Expired certificates are not big issues (I hope)
                return true;

            return false;
        }
    }
}
