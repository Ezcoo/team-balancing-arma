using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace TeamBalanceArma
{
    public static class Players
    {
        public static ConcurrentDictionary<long, Player> ActivePlayers { get; set; }

        static Players()
        {
            Players.ActivePlayers = new ConcurrentDictionary<long, Player>();
        }

        public static string ListPlayersOnServer(List<long> playerUiDsAndSides)
        {
            _ = Task.Run(() =>
            {
                try
                {

                    FlushSideInfoOfAllPlayers();

                    ConcurrentDictionary<long, long> players = new ConcurrentDictionary<long, long>();

                    UpdateActivePlayerStats(playerUiDsAndSides, players);

                    return "[1]";
                }
                catch (MySqlException error)
                {
                    Log.Write(error.Number + ", " + error.ToString(), LogLevel.ERROR);
                    return "[-111]";
                }
            });

            return "[1]";
        }

        public static void FlushSideInfoOfAllPlayers()
        {
            try
            {

                MySqlCommand cmd = new MySqlCommand();

                using (var conn = new MySql.Data.MySqlClient.MySqlConnection())
                {
                    conn.ConnectionString = GlobalVariables.DbLocationAndDetails;
                    conn.Open();

                    cmd.Connection = conn;

                    cmd.CommandText = "flush_players";
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.ExecuteNonQuery();

                    Log.Write("Sides of all players were flushed (just to make sure because of Arma...)", LogLevel.DEBUG);

                    conn.Close();
                }
            }
            catch (MySqlException error)
            {
                Log.Write(error.Number + ", " + error.ToString(), LogLevel.ERROR);
            }
        }

        // 'activePlayersTemp' variable exists because we need to prepare (and finish preparing!) a dictionary before it can be used by the next methods (to prevent race conditions)
        // It's better to keep the variable here instead of e.g. as object/class variable to prevent data corruption accidentally
        // It's probably better to minimize the noticeable risk of extra bugs instead of minor optimization
        private static void UpdateActivePlayerStats(List<long> playerUiDsAndSides, ConcurrentDictionary<long, long> activePlayersTemp)
        {
            try
            {
                activePlayersTemp.Clear();

                Log.Write("ActivePlayersTemp was cleared.", LogLevel.VERBOSE);

                for (int playerUidIndex = 1; playerUidIndex < playerUiDsAndSides.Count - 1; playerUidIndex++)
                {
                    if (playerUidIndex % 2 == 1)
                    {
                        int sideIndex = playerUidIndex + 1;

                        try
                        {
                            activePlayersTemp.TryAdd(playerUiDsAndSides[playerUidIndex], playerUiDsAndSides[sideIndex]);
                            Log.Write("Player (UID) and their side were added to ActivePlayersTemp. Player UID: " + playerUiDsAndSides[playerUidIndex] + ", player side as number: " + playerUiDsAndSides[sideIndex], LogLevel.VERBOSE);
                        }
                        catch (Exception error)
                        {
                            Log.Write("Tried to insert key: " + playerUiDsAndSides[playerUidIndex] + " to index: " + playerUidIndex + " with value: " + playerUiDsAndSides[sideIndex] + " but the operation failed with error: " + error.ToString(), LogLevel.WARNING);
                        }
                    }
                }

                Log.Write("Inserting active player data...", LogLevel.VERBOSE);
                InsertActivePlayerData(activePlayersTemp);
            }
            catch (Exception error)
            {
                Log.Write(error.ToString(), LogLevel.ERROR);
            }

        }

        private static void InsertActivePlayerData(ConcurrentDictionary<long, long> activePlayersTemp)
        {
            try
            {

                ActivePlayers.Clear();
                Log.Write("ActivePlayers was cleared.", LogLevel.VERBOSE);

                foreach (long guid in activePlayersTemp.Keys)
                {

                    try
                    {
                        bool playerIsActiveNow = Players.ActivePlayers.TryGetValue(guid, out Player selectedPlayer);

                        Player player = null;

                        if (playerIsActiveNow)
                        {
                            player = selectedPlayer;
                        }
                        else
                        {
                            player = new Player(guid);
                            ActivePlayers.TryAdd(guid, player);
                        }

                        ActivePlayers.TryGetValue(guid, out Player selectedPlayerTemp);
                        activePlayersTemp.TryGetValue(guid, out long sideAsNumber);

                        Log.Write("Storing player " + guid + " side as number: " + sideAsNumber, LogLevel.VERBOSE);
                        selectedPlayerTemp?.StoreSide(sideAsNumber);

                    }
                    catch (Exception error)
                    {
                        Log.Write("Tried to insert GUID: " + guid + " to database to activePlayers variable but the operation failed: " + error.ToString(), LogLevel.ERROR);
                    }
                }

            }
            catch (MySqlException error)
            {
                Log.Write(error.Number + ", " + error.ToString(), LogLevel.ERROR);
            }
        }

        public static string StoreSideOfPlayer(List<long> arguments)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    long guid = arguments[1];
                    long sideAsNumber = arguments.Last();

                    Log.Write("Trying to find a player with GUID: " + guid + " and update their side to: " + sideAsNumber, LogLevel.VERBOSE);

                    bool playerIsActiveNow = Players.ActivePlayers.TryGetValue(guid, out Player player);

                    Player selectedPlayer = null;

                    if (playerIsActiveNow)
                    {
                        selectedPlayer = player;
                    }

                    if (selectedPlayer != null)
                    {
                        if (selectedPlayer.StoreSide(sideAsNumber))
                        {
                            Log.Write("The side of player with GUID: " + guid + " has been added. Current side of player: " + selectedPlayer.GetCurrentSide(), LogLevel.VERBOSE);
                            return "[1]";
                        }

                        Log.Write("Saving the side of player with GUID: " + guid + " failed! Current side of player: " + selectedPlayer.GetCurrentSide(), LogLevel.WARNING);
                        return "[-111]";
                    }
                    else
                    {
                        Log.Write("The current side of player with GUID: " + guid + " couldn't be stored because player object wasn't found from internal list of active players!", LogLevel.DEBUG);

                        // We need to do hacky bug prevention / data corruption prevention like this because of Arma...
                        Thread.Sleep(500);

                        Log.Write("Trying to find the player with GUID: " + guid + " again after extra sleep of 500 ms.", LogLevel.DEBUG);

                        bool playerIsActiveNowNewTry = Players.ActivePlayers.TryGetValue(guid, out Player playerNewTry);

                        if (playerIsActiveNowNewTry)
                        {
                            var selectedPlayerNewTry = playerNewTry;
                            
                            if (selectedPlayerNewTry.StoreSide(sideAsNumber))
                            {
                                Log.Write("The side of a new player with GUID: " + guid + " has been added. Current side of player: " + selectedPlayer.GetCurrentSide(), LogLevel.VERBOSE);
                                return "[1]";
                            }

                            Log.Write("Saving the side of a new player with GUID: " + guid + " failed! Current side of player: " + selectedPlayer.GetCurrentSide(), LogLevel.WARNING);
                            return "[-111]";
                        }
                        else
                        {
                            Player newPlayer = new Player(guid);

                            ActivePlayers.TryAdd(guid, newPlayer);

                            Log.Write("Player with GUID: " + guid + " was added to internal list of active players.", LogLevel.VERBOSE);

                            if (newPlayer.StoreSide(sideAsNumber))
                            {
                                Log.Write("The side of player with GUID: " + guid + " has been added. Current side of player: " + selectedPlayer.GetCurrentSide(), LogLevel.VERBOSE);
                                return "[1]";
                            }

                            Log.Write("Saving the side of player with GUID: " + guid + " failed! Current side of player: " + selectedPlayer.GetCurrentSide(), LogLevel.WARNING);
                            return "[-111]";
                        }
                    }

                }
                catch (Exception error)
                {
                    Log.Write("ERROR during storing player side. Error: " + error.ToString(), LogLevel.DEBUG);
                    return "[-111]";
                }
            });

            return "[1]";
        }
    }
}