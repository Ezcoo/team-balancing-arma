using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace TeamBalanceArma
{
    public static class PlayerSkill
    {
        // Using dictionary/hashmap for performance reasons (instead of array), no need to loop through the array then - O(n) = 1
        private static readonly ConcurrentDictionary<string, string> ActiveScoreRequestsAndIDs = new ConcurrentDictionary<string, string>();
        private static readonly ConcurrentDictionary<string, string> ActiveTeamSkillRequestsAndIDs = new ConcurrentDictionary<string, string>();

        public static string RetrievePlayer(List<long> args)
        {
            // Generate a random request ID for Arma to check with regular intervals until the request from DB is ready
            // This is necessary because calling external DLLs in Arma is kind of complicated and has limited functionality
            // Range of request IDs is limited because every number in Arma is float internally, and the 32-bit precision doesn't handle values exceeding 999999 reliably
            string requestId = GlobalVariables.Random.Next(0, 899999).ToString();

            // Make sure that we won't have two data requests with the same ID
            while (ActiveScoreRequestsAndIDs.ContainsKey(requestId))
            {
                requestId = GlobalVariables.Random.Next(0, 999999).ToString();
            }

            ActiveScoreRequestsAndIDs.TryAdd(requestId, "-1");

            _ = Task.Run(() =>
            {
                try
                {
                    long selectedGuid = args[1];

                    Log.Write("Initializing player data request. Request ID: " + requestId + ", player GUID: " + selectedGuid, LogLevel.VERBOSE);

                    return RetrievePlayerData(selectedGuid, requestId);
                }
                catch (MySqlException error)
                {
                    Log.Write(error.Number + ", " + error.ToString(), LogLevel.ERROR);
                    return "[1, " + requestId + "]";
                }

            });
            return "[1," + requestId + "]";
        }


        private static string RetrievePlayerData(long guid, string requestId)
        {

            _ = Task.Run(() =>
            {
                try
                {

                    Log.Write("Trying to create a player entry... GUID: " + guid, LogLevel.VERBOSE);

                    bool playerIsActiveNow = Players.ActivePlayers.TryGetValue(guid, out Player selectedPlayer);

                    Player player = null;

                    if (playerIsActiveNow)
                    {
                        player = selectedPlayer;
                    }

                    if (player == null)
                    // Player doesn't exist yet in the database, so we have to create the entry.
                    {
                        Log.Write("Player with GUID: " + guid + " was not found from database.", LogLevel.INFO);

                        Player newPlayer = new Player(guid);

                        Players.ActivePlayers.TryAdd(guid, newPlayer);

                        Log.Write("Player with GUID: " + guid + " was added to internal list of active players.", LogLevel.VERBOSE);

                        Log.Write("Player with GUID: " + guid + " was inserted into the database for the first time. Player total skill: " + newPlayer.GetSkillAsDouble() + ", IsNewPlayer(): " + newPlayer.IsNewPlayer(), LogLevel.VERBOSE);

                        if (!(ActiveScoreRequestsAndIDs.ContainsKey(requestId)))
                        {
                            Log.Write("Adding request ID " + requestId + " to queue with value: " + newPlayer.GetStatsAsString(), LogLevel.VERBOSE);
                            ActiveScoreRequestsAndIDs.TryAdd(requestId, newPlayer.GetStatsAsString());
                        }
                        else
                        {
                            ActiveScoreRequestsAndIDs.TryGetValue(requestId, out string playerStats);
                            Log.Write("Request ID " + requestId + " exists already in the queue. Request payload:" + playerStats, LogLevel.VERBOSE);
                            
                            if (playerStats != null && playerStats.Equals("-1"))
                            {
                                Log.Write("Request ID: " + requestId + " is being processed but the result is not ready yet.", LogLevel.DEBUG);
                                ActiveScoreRequestsAndIDs[requestId] = newPlayer.GetStatsAsString();
                            }
                        }
                    }
                    else
                    {
                        Player existingPlayer = player;

                        bool existingPlayerIsNew = existingPlayer.IsNewPlayer();

                        if (existingPlayerIsNew)
                        {
                            Log.Write("Player with GUID: " + guid + " was found from database. Player is considered a new player (default skill value is: " + existingPlayer.GetSkillAsDouble() + ").", LogLevel.DEBUG);
                        }
                        else
                        {
                            Log.Write("Player with GUID: " + guid + " was found from database. Player is considered an old player. Player's skill: " + existingPlayer.GetSkillAsDouble() + ".", LogLevel.DEBUG);
                        }

                        /*
                        foreach (KeyValuePair<string, string> pair in activeRequestIDsAndResults)
                        {
                            Log.Write("activeRequestIDsAndResults, key:  " + pair.Key + ", value: " + pair.Value, LogLevel.DEBUG);
                        }
                        */

                        if (!ActiveScoreRequestsAndIDs.ContainsKey(requestId))
                        {
                            Log.Write("Request ID: " + requestId + " was not found from active requests list.", LogLevel.VERBOSE);
                            ActiveScoreRequestsAndIDs.TryAdd(requestId, existingPlayer.GetStatsAsString());
                            Log.Write("Added request ID: " + requestId + " to active requests list.", LogLevel.VERBOSE);
                        }
                        else
                        {
                            ActiveScoreRequestsAndIDs.TryGetValue(requestId, out string playerStats);

                            if (playerStats != null && playerStats.Equals("-1"))
                            {
                                Log.Write("Request ID: " + requestId + " exists and is being processed but the result is not ready yet.", LogLevel.VERBOSE);
                                ActiveScoreRequestsAndIDs[requestId] = existingPlayer.GetStatsAsString();
                            }
                        }
                    }
                }
                catch (MySqlException error)
                {
                    Log.Write(error.Number + ", " + error.ToString(), LogLevel.ERROR);
                }
            });

            return requestId;
        }
        
        // Some cheating prevention to catch the most evident cases - we can't do much more in this DLL/application when it comes to cheating
        private static bool CheckScoreDifferencePassed(long scoreDiff, long guid)
        {
            Log.Write("Player with GUID: " + guid + ", score difference: " + scoreDiff, LogLevel.VERBOSE);

            if (scoreDiff < 0)
            {
                if (scoreDiff < -30)
                {
                    Log.Write("Player with GUID: " + guid + ", score difference is NEGATIVE ( " + scoreDiff + " ). The negative score difference is high, so player is probably trying to cheat (or they're a massive teamkiller). The results were NOT saved to database. This is a severe alert!", LogLevel.IMPORTANT); ;
                    return false;
                }
                else
                {
                    Log.Write("Player with GUID: " + guid + ", score difference is NEGATIVE ( " + scoreDiff + " ). Something might be wrong here! The player is either a teamkiller or trying to cheat. The results were NOT saved to database.", LogLevel.WARNING);
                    return false;
                }
            }

            if (scoreDiff > 2000)
            {
                Log.Write("Player with GUID: " + guid + ", scoreDiff is HUGE: (" + scoreDiff + " )! Player is cheating extremely likely. The results were NOT saved to database. This is a severe alert!", LogLevel.IMPORTANT);
                return false;
            }

            return true;
        }

        public static string StorePlayer(List<long> args)
        {
            _ = Task.Run(() =>
            {

                try
                {
                    long guid = args[1];
                    long scoreDiff = args[2];

                    Log.Write("StorePlayerSkill was called, parameters: GUID: " + guid + ", score difference: " + scoreDiff, LogLevel.VERBOSE);

                    if (CheckScoreDifferencePassed(scoreDiff, guid))
                    {
                        return StorePlayerStats(scoreDiff, guid);

                    }
                    else
                    {

                        return "[-222,0,1]";

                    }

                }
                catch (MySqlException error)
                {
                    Log.Write(error.Number + ", " + error.ToString(), LogLevel.ERROR);
                    return "[-111, 1, 1]";
                }
            });

            return "[1,1,1]";
        }

        private static string StorePlayerStats(long scoreDiff, long guid)
        {

            _ = Task.Run(() =>
            {
                try
                {

                    Log.Write("Trying to create a player entry...", LogLevel.VERBOSE);

                    bool playerIsActiveNow = Players.ActivePlayers.TryGetValue(guid, out Player selectedPlayer);

                    Player player = null;

                    if (playerIsActiveNow)
                    {
                        player = selectedPlayer;
                    }

                    // If player doesn't exist, we want to create a new entry for them.
                    if (player == null)
                    {
                        Log.Write("Player with GUID: " + guid + " was not found from internal list of active players.", LogLevel.VERBOSE);
                        Player newPlayer = new Player(guid);

                        newPlayer.AddScore(scoreDiff);

                        Log.Write("Player with GUID: " + guid + " was inserted into the database. Player total skill: " + newPlayer.GetSkillAsDouble(), LogLevel.VERBOSE);

                        Players.ActivePlayers.TryAdd(guid, newPlayer);

                        Log.Write("Player with GUID: " + guid + " was added to internal list of active players.", LogLevel.VERBOSE);
                    }
                    else
                    {
                        Player existingPlayer = player;

                        bool isNewPlayer = existingPlayer.IsNewPlayer();

                        Log.Write("Player with GUID: " + guid + " was found. Player total skill: " + existingPlayer.GetSkillAsDouble() + ", is new player: " + isNewPlayer, LogLevel.VERBOSE);

                        existingPlayer.AddScore(scoreDiff);

                        Log.Write("Player with GUID: " + guid + ", score difference is: " + scoreDiff, LogLevel.VERBOSE);

                        Log.Write("Player with GUID: " + guid + " was updated to database, total score: " + existingPlayer.GetTotalScore() + ", ticks: " + existingPlayer.GetTotalTicks() + ", is new player: " + isNewPlayer + ", new skill level: " + existingPlayer.GetSkillAsDouble(), LogLevel.DEBUG);

                    }
                }
                catch (MySqlException error)
                {
                    Log.Write(error.Number + ", " + error.ToString(), LogLevel.ERROR);
                }
            });

            return "[2]";

        }

        public static string TryRetrieve(List<long> args)
        {
            const string nothingToReturn = "[-1]";

            try
            {
                string requestId = args[1].ToString();

                if (ActiveScoreRequestsAndIDs.ContainsKey(requestId))
                {
                    if (ActiveScoreRequestsAndIDs.TryGetValue(requestId, out string result))
                    {
                        if (result.Equals("-1"))
                        {
                            Log.Write("Player data request with ID: " + requestId + " is not ready yet.", LogLevel.DEBUG);
                            return nothingToReturn;
                        }
                        // We have a result!
                        else
                        {
                            Log.Write("Player data request with ID " + requestId + " is ready! Returning value: " + result + ".", LogLevel.VERBOSE);
                            ActiveScoreRequestsAndIDs.TryRemove(requestId, out string payload);
                            string returnString = "[1," + result + "]";
                            Log.Write("The exact string being returned with player data request ID: " + requestId + " is: " + returnString, LogLevel.VERBOSE);
                            return returnString;
                        }
                    }
                }
            }
            catch (MySqlException error)
            {
                Log.Write(error.Number + ", " + error.ToString(), LogLevel.ERROR);
            }

            return nothingToReturn;
        }


        public static string GetSideTotalSkill(List<long> arguments)
        {
            string requestId = GlobalVariables.Random.Next(900000, 999999).ToString();

            // Make sure that we won't have two data requests with the same ID
            while (ActiveTeamSkillRequestsAndIDs.ContainsKey(requestId))
            {
                requestId = GlobalVariables.Random.Next(900000, 999999).ToString();
            }

            ActiveTeamSkillRequestsAndIDs.TryAdd(requestId, "-1");

            _ = Task.Run(() =>
            {
                double sideTotalSkill = 0;
                Side side = Side.NONE;

                try
                {
                    long sideAsNumber = arguments.Last();

                    if (sideAsNumber == 1)
                    {
                        side = Side.WEST;
                    }
                    else if (sideAsNumber == 2)
                    {
                        side = Side.EAST;
                    }

                    List<Player> activePlayersOnSide = GetPlayersOnSide(side);

                    foreach (Player player in activePlayersOnSide)
                    {
                        sideTotalSkill += (double)player.GetTotalScore() / player.GetTotalTicks();
                    }

                    sideTotalSkill = Math.Round(sideTotalSkill, 1);

                    bool teamSkillRequestExists = ActiveTeamSkillRequestsAndIDs.TryGetValue(requestId, out string sideTotalSkillValue);

                    if (teamSkillRequestExists)
                    {
                        if (sideTotalSkillValue == "-1")
                        {
                            ActiveTeamSkillRequestsAndIDs[requestId] = sideTotalSkill.ToString();
                        }
                    } 
                    else
                    {
                        ActiveTeamSkillRequestsAndIDs.TryAdd(requestId, sideTotalSkill.ToString());
                    }
                }
                catch (Exception error)
                {
                    Log.Write("Couldn't retrieve side [" + side + "] total skill! Error: " + error.ToString(), LogLevel.ERROR);
                }
            });

            return "[1," + requestId + "]";
        }

        public static List<Player> GetPlayersOnSide(Side side)
        {
            List<Player> listOfPlayersOnSide = new List<Player>();

            try
            {
                using (var conn = new MySql.Data.MySqlClient.MySqlConnection())
                {
                    conn.ConnectionString = GlobalVariables.DbLocationAndDetails;
                    conn.Open();

                    MySqlCommand cmd = new MySqlCommand();
                    cmd.Connection = conn;
                    Log.Write("Connection opened and attached to MySqlCommand.", LogLevel.VERBOSE);

                    cmd.CommandText = "get_active_players_side";
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@requestedSide", side.ToString());
                    cmd.Parameters["@requestedSide"].Direction = ParameterDirection.Input;

                    MySqlDataReader reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        listOfPlayersOnSide.Add(new Player(reader.GetInt64(reader.GetOrdinal("player_guid"))));
                    }

                    reader.Close();

                    conn.Close();

                    return listOfPlayersOnSide;
                }
            }
            catch (Exception error)
            {
                Log.Write("Couldn't retrieve side [" + side + "] active players! Error: " + error.ToString(), LogLevel.ERROR);
                return null;
            }
        }

        public static string TryRetrieveSideTotalSkill(List<long> args)
        {
            string nothingToReturn = "[-1]";

            try
            {
                string requestId = args[1].ToString();

                if (ActiveTeamSkillRequestsAndIDs.ContainsKey(requestId))
                {
                    if (ActiveTeamSkillRequestsAndIDs.TryGetValue(requestId, out string result))
                    {
                        if (result.Equals("-1"))
                        {
                            Log.Write("Team total skill request with ID: " + requestId + " is not ready yet.", LogLevel.DEBUG);
                            return nothingToReturn;
                        }
                        // We have a result!
                        else
                        {
                            string receivedValue = result;
                            Log.Write("Team total skill request with ID " + requestId + " is ready! Returning value: " + receivedValue + ".", LogLevel.VERBOSE);
                            ActiveTeamSkillRequestsAndIDs.TryRemove(requestId, out string payload);
                            string returnString = "[1," + receivedValue + "]";
                            Log.Write("The exact string being returned with team total skill request ID: " + requestId + " is: " + returnString, LogLevel.VERBOSE);
                            return returnString;
                        }
                    }
                }
            }
            catch (Exception error)
            {
                Log.Write(error.ToString(), LogLevel.ERROR);
            }

            return nothingToReturn;
        }
    }
}