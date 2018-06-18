using System;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using log4net;
using Newtonsoft.Json;

namespace Negri.Wot.Sql
{
    /// <summary>
    /// To retrieve and save data from the Sql Key Store
    /// </summary>
    public class KeyStore : DataAccessBase
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(KeyStore));

        /// <inheritdoc />
        public KeyStore(string connectionString) : base(connectionString)
        {
        }

        /// <summary>
        /// Set a player
        /// </summary>
        public void Set(Player player)
        {
            if (player == null)
            {
                return;
            }

            Log.Debug($"Seting player {player.Id}...");
            var sw = Stopwatch.StartNew();
            Execute(transaction => { Set(player, transaction); }, 3);
            Log.Debug($"Set player {player.Id} done in {sw.Elapsed}.");
        }

        private static void Set(Player player, SqlTransaction t)
        {
            var json = JsonConvert.SerializeObject(player, Formatting.None);
            var bin = json.Zip();

            using (var cmd = new SqlCommand("Store.SetPlayer", t.Connection, t))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@playerId", player.Id);
                cmd.Parameters.AddWithValue("@data", bin);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Retrieve a player
        /// </summary>
        public Player GetPlayer(long playerId)
        {
            Log.Debug($"Retrieving player {playerId}...");
            var sw = Stopwatch.StartNew();
            var p = Get(t => GetPlayer(playerId, t), 3);
            Log.Debug($"Retrieved player {playerId} ({p?.Name ?? string.Empty}) in {sw.Elapsed}.");
            return p;
        }

        private static Player GetPlayer(long playerId, SqlTransaction t)
        {
            const string sql = "select BinData from Store.Player where PlayerId = @playerId;";
            using (var cmd = new SqlCommand(sql, t.Connection, t))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 20;
                cmd.Parameters.AddWithValue("@playerId", playerId);                
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var bin = (byte[])reader[0];
                        var json = bin.Unzip();
                        return JsonConvert.DeserializeObject<Player>(json);
                    }
                }
            }

            // not found
            return null;
        }
    }
}