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
        /// Clean from the database players that are not being updated 
        /// </summary>
        /// <param name="age">Age (in days) of the stale records to be cleaned</param>
        public void CleanOldPlayers(int age = 63)
        {
            Log.Debug($"Cleaning old players with age > {age}...");
            var sw = Stopwatch.StartNew();

            // No transactions on this case, as any old record deleted should remain deleted anyway
            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("Store.ClearPlayers", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandTimeout = 2 * 60;
                    cmd.Parameters.AddWithValue("@age", age);
                    cmd.ExecuteNonQuery();
                }
            }
            
            Log.Debug($"Cleaned old players with age > {age} in {sw.Elapsed}.");
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

            Log.Debug($"Setting player {player.Id}...");
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