using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Threading;
using log4net;

namespace Negri.Wot
{
    /// <summary>
    ///     To put data on the remote server
    /// </summary>
    public class Putter
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Putter));
        private readonly string _apiKey;
        private readonly string _baseUrl;
        
        public Putter(Platform platform, string apiKey)
        {
            switch (platform)
            {
                case Platform.XBOX:
                    _baseUrl = "https://wotclans.com.br";
                    break;
                case Platform.PS:
                    _baseUrl = "https://ps.wotclans.com.br";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(platform), platform, "Not supported.");
            }            
            _apiKey = apiKey;
        }

        public Putter(string baseUrl, string apiKey)
        {
            _baseUrl = baseUrl;
            _apiKey = apiKey;
        }

        private static void Execute(Action action, int maxTries = 10)
        {
            Exception lastException = null;
            for (var i = 0; i < maxTries; ++i)
            {
                try
                {
                    action();
                    return;
                }
                catch (Exception ex)
                {
                    if (i < maxTries - 1)
                    {
                        Log.Warn(ex);
                        Log.Warn("...esperando antes de tentar novamente.");
                        Thread.Sleep(TimeSpan.FromSeconds(i * i * 2));
                    }
                    else
                    {
                        Log.Error(ex);
                    }

                    lastException = ex;
                }
            }

            if (lastException != null)
            {
                throw lastException;
            }
        }

        public bool Put(Player player)
        {
            if (player.HasMedals)
            {
                // Don't save medals on the remote server, as it will take too much space and there is no use to it (so far)
                var copy = (Player) player.Clone();
                copy.PurgeMedals();
                player = copy;
            }

            try
            {
                Execute(() =>
                {
                    var client = new HttpClient
                    {
                        BaseAddress = new Uri(_baseUrl)
                    };
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/bson"));

                    var req = new PutDataRequest
                    {
                        ApiKey = _apiKey,
                        Context = "Player"
                    };
                    req.SetObject(player);

                    var bsonFormatter = new BsonMediaTypeFormatter();
                    var res = client.PutAsync("api/admin/Data", req, bsonFormatter).Result;
                    res.EnsureSuccessStatusCode();
                }, 5);

                return true;
            }
            catch(Exception ex)
            {
                Log.Error($"Put(Player({player.Id}))", ex);
                return false;
            }
            
        }

        /// <summary>
        /// Clean files on the remote server
        /// </summary>
        public void CleanFiles()
        {
            Execute(() =>
            {
                Log.Debug("Calling CleanFiles API...");

                var client = new HttpClient
                {
                    BaseAddress = new Uri(_baseUrl)
                };
                var res = client.DeleteAsync($"api/admin/CleanDataFolders?apiAdminKey={_apiKey}").Result;
               
                if (res.StatusCode == HttpStatusCode.OK)
                {
                    Log.Debug("Remote deletion is done.");
                }
                else
                {
                    Log.Warn($"Remote deletion fail: {res.StatusCode}, {res.ReasonPhrase}");
                }

            });
        }
    }
}