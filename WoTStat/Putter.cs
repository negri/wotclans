using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Threading;
using log4net;
using Negri.Wot.Tanks;
using Newtonsoft.Json;

namespace Negri.Wot
{
    /// <summary>
    ///     To put data on the remote server
    /// </summary>
    public class Putter
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Putter));

        /// <summary>
        /// The only and only HTTP Client
        /// </summary>
        private static readonly HttpClient HttpClient;

        public string BaseUrl { get; set; } = "https://wotclans.com.br";

        private readonly string _apiKey;

        static Putter()
        {
            HttpClient = new HttpClient();
            HttpClient.DefaultRequestHeaders.Clear();
            HttpClient.DefaultRequestHeaders.Add("user-agent", "WoTClansBr by JP Negri at negrijp _at_ gmail.com");
        }
        
        public Putter(string apiKey)
        {
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

        private static T Get<T>(Func<T> getter, int maxTries = 10)
        {
            Exception lastException = null;
            for (var i = 0; i < maxTries; ++i)
            {
                try
                {
                    return getter();
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

            return default;
        }


        public bool Put(Player player)
        {
            var copy = player;
            if (player.HasMedals)
            {
                // Don't save medals on the remote server, as it will take too much space and there is no use to it (so far)
                copy = (Player) player.Clone();
                copy.PurgeMedals();
            }

            try
            {
                Execute(() =>
                {
                    
                    var req = new PutDataRequest
                    {
                        ApiKey = _apiKey,
                        Context = PutDataRequestContext.Player
                    };
                    req.SetObject(copy);

                    var bsonFormatter = new BsonMediaTypeFormatter();
                    var res = HttpClient.PutAsync($"{BaseUrl}/api/admin/Data", req, bsonFormatter).Result;
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

        public bool Put(TankReference tankReference)
        {
            try
            {
                Execute(() =>
                {

                    var req = new PutDataRequest
                    {
                        ApiKey = _apiKey,
                        Context = PutDataRequestContext.TankReference
                    };
                    req.SetObject(tankReference);

                    var bsonFormatter = new BsonMediaTypeFormatter();
                    var res = HttpClient.PutAsync($"{BaseUrl}/api/admin/Data", req, bsonFormatter).Result;
                    res.EnsureSuccessStatusCode();
                }, 5);

                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Put({tankReference.Name}))", ex);
                return false;
            }
        }

        public bool Put(Clan clan)
        {
            try
            {
                Execute(() =>
                {

                    var req = new PutDataRequest
                    {
                        ApiKey = _apiKey,
                        Context = PutDataRequestContext.Clan
                    };
                    req.SetObject(clan);

                    var bsonFormatter = new BsonMediaTypeFormatter();
                    var res = HttpClient.PutAsync($"{BaseUrl}/api/admin/Data", req, bsonFormatter).Result;
                    res.EnsureSuccessStatusCode();
                }, 5);

                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Put({clan.Name}))", ex);
                return false;
            }
        }

        public bool Put(DateTime date, IEnumerable<Leader> leaders)
        {
            try
            {
                Execute(() =>
                {

                    var req = new PutDataRequest
                    {
                        ApiKey = _apiKey,
                        Context = PutDataRequestContext.Leaderboard,
                        Title = date.ToString("yyyy-MM-dd")
                    };
                    req.SetObject(leaders.ToArray());

                    var bsonFormatter = new BsonMediaTypeFormatter();
                    var res = HttpClient.PutAsync($"{BaseUrl}/api/admin/Data", req, bsonFormatter).Result;
                    res.EnsureSuccessStatusCode();
                }, 5);

                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Put({date:yyyy-MM-dd}, Leaders))", ex);
                return false;
            }
        }



        /// <summary>
        /// Clean files on the remote server
        /// </summary>
        public CleanOldDataResponse CleanOldData(
            int daysToKeepOnDaily = 4 * 7 + 7,
            int daysToKeepOnWeekly = 3 * 4 * 7 + 7,
            int daysToKeepClanFiles = 2 * 4 * 7 + 7,
            int daysToKeepPlayerFiles = 2 * 4 * 7 + 7)
        {
            return Get(() =>
            {
                Log.Debug("Calling CleanOldData API...");

                var url = $"{BaseUrl}/api/admin/CleanDataFolders?apiAdminKey={_apiKey}&daysToKeepOnDaily={daysToKeepOnDaily}&" +
                          $"daysToKeepOnWeekly={daysToKeepOnWeekly}&daysToKeepClanFiles={daysToKeepClanFiles}&daysToKeepPlayerFiles={daysToKeepPlayerFiles}";

                var res = HttpClient.DeleteAsync(url).Result;
               
                if (res.StatusCode == HttpStatusCode.OK)
                {
                    Log.Debug("Remote deletion is done.");
                    var json = res.Content.ReadAsStringAsync().Result;
                    var cleaned = JsonConvert.DeserializeObject<CleanOldDataResponse>(json);
                    return cleaned;
                }

                Log.Warn($"Remote deletion fail: {res.StatusCode}, {res.ReasonPhrase}");
                return null;

            });
        }

        
        /// <summary>
        /// Removes a clan from the site
        /// </summary>
        /// <remarks>
        /// For when a clan gets disbanded, so it's reflected on the site as soon as this is detected
        /// </remarks>
        public void DeleteClan(string clanTag)
        {
            Log.Debug("Calling DeleteClan API...");

            var client = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl)
            };
            var res = client.DeleteAsync($"api/admin/DeleteClan?apiAdminKey={_apiKey}&clanTag={clanTag}").Result;

            if (res.StatusCode == HttpStatusCode.OK)
            {
                Log.Debug("Remote deletion is done.");
            }
            else
            {
                Log.Warn($"Remote deletion fail: {res.StatusCode}, {res.ReasonPhrase}");
            }
        }


        
    }
}