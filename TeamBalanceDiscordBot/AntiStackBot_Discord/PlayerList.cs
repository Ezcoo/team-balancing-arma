using Discord;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;

namespace A2WASPDiscordBot_Windows_App
{
    public static class PlayerList
    {
        public static Dictionary<long, Player> players { get; set; }

        static PlayerList()
        {
            players = new Dictionary<long, Player>();
        }

        /*
        public static string GetPlayersAsString()
        {

            ICollection<Player> allPlayersList = GlobalVariables.database.GetCollection<Player>("players").FindAll().ToList<Player>();

            string allPlayersString = "";

            foreach (Player player in allPlayersList)
            {
                allPlayersString += "\n" + player.id + " skill: " + player.GetSkillAsDouble() + "\n";
            }

            Log.Write("Contents of allPlayersString: " + allPlayersString, LogLevel.DEBUG);

            // string allPlayersString = "Not implemented yet! KEK";

            return allPlayersString;
        }
        */

        public static string GetSideWherePlayersShouldJoinString()
        {
            if (GetSideWherePlayerShouldJoin() == Side.WEST)
            {
                return "BLUFOR";
            }
            else
            {
                return "OPFOR";
            }
        }

        public static Side GetSideWherePlayerShouldJoin()
        {
            double skillWest = GetSideTotalSkill(Side.WEST);
            double skillEast = GetSideTotalSkill(Side.EAST);

            if (skillWest < skillEast)
            {
                Log.Write("Team to join is: " + Side.WEST, LogLevel.VERBOSE);
                return Side.WEST;
            }
            else
            {
                Log.Write("Team to join is: " + Side.EAST, LogLevel.VERBOSE);
                return Side.EAST;
            }
        }

        public static double GetTotalSkillOfSide(Side side)
        {
            Log.Write("Starting to count the total skill of side: " + side + ".", LogLevel.DEBUG);

            List<Player> activePlayersOfSide = RetrieveDataOfPlayersOfSide(side);

            if (activePlayersOfSide == null)
            {
                return 0;
            }

            double totalSkillOfTeam = 0;

            foreach (Player player in activePlayersOfSide)
            {
                totalSkillOfTeam += player.GetSkillAsDouble();
            }

            totalSkillOfTeam = Math.Round(totalSkillOfTeam, 2);

            Log.Write("Total skill of side [" + side + "] is: " + totalSkillOfTeam, LogLevel.DEBUG);

            return totalSkillOfTeam;

        }

        public static List<Player> RetrieveDataOfPlayersOfSide(Side side)
        {
            List<Player> listOfActivePlayers = new List<Player>();

            try
            {
                using (var conn = new MySql.Data.MySqlClient.MySqlConnection())
                {
                    conn.ConnectionString = GlobalVariables.dbConnectionString;
                    conn.Open();

                    MySqlCommand cmd = new MySqlCommand();
                    cmd.Connection = conn;
                    // Log.Write("Connection opened and attached to MySqlCommand.", LogLevel.DEBUG);

                    cmd.CommandText = "get_active_players_side";
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@requestedSide", side.ToString());
                    cmd.Parameters["@requestedSide"].Direction = ParameterDirection.Input;

                    MySqlDataReader reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        listOfActivePlayers.Add(new Player(reader.GetInt64(reader.GetOrdinal("player_guid"))));
                    }

                    reader.Close();

                    conn.Close();

                    return listOfActivePlayers;
                }
            }
            catch (MySqlException ex)
            {
                Log.Write("Retrieving data of players on side: " + side.ToString() + " failed! Error code: " + ex.Number + ": " + ex.Message, LogLevel.ERROR);
                return null;
            }
        }

        public static int GetPlayerCountOnSide(Side side)
        {
            int playerCountOnSide = 0;

            try
            {

                using (var conn = new MySql.Data.MySqlClient.MySqlConnection())
                {
                    conn.ConnectionString = GlobalVariables.dbConnectionString;
                    conn.Open();

                    MySqlCommand cmd = new MySqlCommand();
                    cmd.Connection = conn;
                    // Log.Write("Connection opened and attached to MySqlCommand.", LogLevel.DEBUG);

                    cmd.CommandText = "get_active_players_count_side";
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@requestedSide", side.ToString());
                    cmd.Parameters["@requestedSide"].Direction = ParameterDirection.Input;

                    try
                    {
                        cmd.Parameters.Add("@playerCount", MySqlDbType.Int32);
                        cmd.Parameters["@playerCount"].Direction = ParameterDirection.Output;

                        cmd.ExecuteNonQuery();

                        playerCountOnSide = Convert.ToInt32(cmd.Parameters["@playerCount"].Value);
                        conn.Close();

                    }
                    catch (MySqlException e)
                    {
                        Log.Write(e.Number + ", " + e.Message, LogLevel.ERROR);
                        playerCountOnSide = 0;
                        conn.Close();
                    }
                }
            }
            catch (MySqlException ex)
            {
                Log.Write("Retrieving data of players on side: " + side.ToString() + " failed! Error code: " + ex.Number + ": " + ex.Message, LogLevel.ERROR);
                return 0;
            }

            return playerCountOnSide;
        }

        public static Emote GetSideEmote(Side side)
        {
            if (side == Side.WEST)
            {
                return Emote.Parse("<:blufor_icon:1079145826280034344>");
            }
            else
            {
                return Emote.Parse("<:opfor_icon:1079145850418233364>");
            }
        }

        public static double GetSideTotalSkill(Side side)
        {
            double sideTotalSkill = 0;
            // TODO: Move to environment variable
            double playerNumberDifferenceModifier = 0.15;

            List<Player> activePlayersOnSide = GetPlayersOnSide(side);

            foreach (Player player in activePlayersOnSide)
            {
                sideTotalSkill += (double)player.GetTotalScore() / player.GetTotalTicks();
            }

            int playerCountOnBLUFOR = GetPlayerCountOnSide(Side.WEST);
            int playerCountOnOPFOR = GetPlayerCountOnSide(Side.EAST);

            int playerNumberDifferenceBLUFOR = playerCountOnBLUFOR - playerCountOnOPFOR;
            int playerNumberDifferenceOPFOR = playerCountOnOPFOR - playerCountOnBLUFOR;

            double differenceCoefficient = 0;

            if (playerNumberDifferenceBLUFOR > 0 && (playerCountOnBLUFOR + playerCountOnOPFOR < 8))
            {
                differenceCoefficient = playerNumberDifferenceBLUFOR * playerNumberDifferenceModifier * 2;
                sideTotalSkill = sideTotalSkill * (1 + differenceCoefficient);
            }
            else if (playerNumberDifferenceBLUFOR < 0 && (playerCountOnBLUFOR + playerCountOnOPFOR) < 12)
            {
                differenceCoefficient = playerNumberDifferenceBLUFOR * playerNumberDifferenceModifier;
            } else
            {
                differenceCoefficient = 0;
            }

            if (playerNumberDifferenceOPFOR > 0 && (playerCountOnBLUFOR + playerCountOnOPFOR) < 8)
            {
                differenceCoefficient += playerNumberDifferenceOPFOR * playerNumberDifferenceModifier * 2;
                sideTotalSkill = sideTotalSkill * (1 + differenceCoefficient);
            } else if (playerNumberDifferenceOPFOR > 0 && (playerCountOnBLUFOR + playerCountOnOPFOR) < 12)
            {
                differenceCoefficient = playerNumberDifferenceOPFOR * playerNumberDifferenceModifier;
                sideTotalSkill = sideTotalSkill * (1 + differenceCoefficient);
            } else
            {
                differenceCoefficient = 0;
            }

            sideTotalSkill = Math.Round(sideTotalSkill, 1);

            return sideTotalSkill;
        }

        public static List<Player> GetPlayersOnSide(Side side)
        {
            List<Player> listOfPlayersOnSide = new List<Player>();

            try
            {
                using (var conn = new MySql.Data.MySqlClient.MySqlConnection())
                {
                    conn.ConnectionString = GlobalVariables.dbConnectionString;
                    conn.Open();

                    MySqlCommand cmd = new MySqlCommand();
                    cmd.Connection = conn;
                    // Log.Write("Connection opened and attached to MySqlCommand.", LogLevel.DEBUG);

                    cmd.CommandText = "get_active_players_side";
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@requestedSide", side.ToString());
                    cmd.Parameters["@requestedSide"].Direction = ParameterDirection.Input;

                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {

                        while (reader.Read())
                        {
                            listOfPlayersOnSide.Add(new Player(reader.GetInt64(reader.GetOrdinal("player_guid"))));
                        }

                        reader.Close();
                    }

                    conn.Close();

                    return listOfPlayersOnSide;
                }
            }
            catch (Exception e)
            {
                Log.Write("Couldn't retrieve side [" + side + "] active players! Error: " + e.ToString(), LogLevel.ERROR);
                return null;
            }
        }
    }
}