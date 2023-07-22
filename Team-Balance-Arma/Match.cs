using System.Data;
using MySql.Data.MySqlClient;

namespace TeamBalanceArma
{
    static class Match
    {
        static Match()
        {
        }

        public static string SaveMap(string map)
        {
            try
            {
                using (var conn = new MySql.Data.MySqlClient.MySqlConnection())
                {
                    conn.ConnectionString = GlobalVariables.DbLocationAndDetails;
                    conn.Open();

                    MySqlCommand cmd = new MySqlCommand();
                    cmd.Connection = conn;

                    cmd.CommandText = "set_map";
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@currentMap", map);
                    cmd.Parameters["@currentMap"].Direction = ParameterDirection.Input;

                    cmd.ExecuteNonQuery();
                    conn.Close();

                    Log.Write("Current map set! Map: " + map, LogLevel.INFO);

                    return "[1]";
                }
            }
            catch (MySqlException e)
            {
                Log.Write(e.Number + ", " + e.Message, LogLevel.ERROR);
                return "[-1]";
            }
        }

        public static string ConvertLongToMap(long mapId)
        {
            switch (mapId)
            {
                case 0: return "None";
                case 1: return "Chernarus";
                case 2: return "Takistan";

                default: return "None";
            }
        }

    }
}
