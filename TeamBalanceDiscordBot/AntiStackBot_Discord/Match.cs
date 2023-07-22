using A2WASPDiscordBot_Windows_App;
using MySql.Data.MySqlClient;
using System;
using System.Data;

namespace AntiStackBot_Discord
{
    static class Match
    {
        public static string GetMap()
        {
            string map = "None";
            try
            {
                using (var conn = new MySql.Data.MySqlClient.MySqlConnection())
                {
                    conn.ConnectionString = GlobalVariables.dbConnectionString;
                    conn.Open();

                    MySqlCommand cmd = new MySqlCommand();
                    cmd.Connection = conn;

                    cmd.CommandText = "get_map";
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add("@currentMapOut", MySqlDbType.String);
                    cmd.Parameters["@currentMapOut"].Direction = ParameterDirection.Output;

                    cmd.ExecuteNonQuery();

                    map = Convert.ToString(cmd.Parameters["@currentMapOut"].Value);

                    conn.Close();

                    return map;
                }
            }
            catch (MySqlException e)
            {
                Log.Write(e.Number + ", " + e.Message, LogLevel.ERROR);
                map = "None";
            }

            return map;
        }
    }
}
