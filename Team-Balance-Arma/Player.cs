using System;
using System.Collections.Generic;
using System.Data;
using MySql.Data.MySqlClient;

namespace TeamBalanceArma
{
    // We should really use object relational mapper (ORM) here like the (legacy) EF Framework to prevent data corruption!
    // The current state of things is very bad practice since the database and this DLL/extension have to be synced manually now, as the process is very error-prone and requires more testing
    // The reason why we're doing this is simply lack of time to learn the legacy EF framework... The tragedy of hobby projects!
    public class Player
    {
        public long Id { get; set; }
        public List<long> ScoresList { get; set; }
        public Side Side { get; set; }
        private readonly long _maxNumberOfScores;
        private readonly long _ticksThresholdForCountingSkill;
        private readonly long _defaultNumberOfTicks;
        private readonly int _defaultScore;

        public Player(long id)
        {
            Log.Write("Initializing a (possibly) new player object with GUID: " + id, LogLevel.VERBOSE);
            this.Id = id;
            this.ScoresList = new List<long>();
            this._maxNumberOfScores = 500;
            this._ticksThresholdForCountingSkill = 40;
            this._defaultNumberOfTicks = 10;
            this._defaultScore = 40;
            this.Side = Side.NONE;
            ExistsInDatabase(this.Id);
        }

        public bool ExistsInDatabase(long guid)
        {
            try
            {
                using (var conn = new MySql.Data.MySqlClient.MySqlConnection())
                {
                    conn.ConnectionString = GlobalVariables.DbLocationAndDetails;
                    conn.Open();

                    MySqlCommand cmd = new MySqlCommand();
                    cmd.Connection = conn;
                    // Log.Write("Connection opened and attached to MySqlCommand.", LogLevel.DEBUG);

                    cmd.CommandText = "check_if_player_exists";
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@guid", guid);
                    cmd.Parameters["@guid"].Direction = ParameterDirection.Input;

                    int boolAsInt = 0;
                    try
                    {
                        cmd.Parameters.Add("@result", MySqlDbType.Int32);
                        cmd.Parameters["@result"].Direction = ParameterDirection.Output;

                        cmd.ExecuteNonQuery();

                        boolAsInt = Convert.ToInt32(cmd.Parameters["@result"].Value);
                        conn.Close();

                    }
                    catch (MySqlException error)
                    {
                        Log.Write(error.Number + ", " + error.Message, LogLevel.ERROR);
                        boolAsInt = 0;
                        conn.Close();
                    }

                    if (boolAsInt > 0)
                    {
                        Log.Write("Player was found from the database.", LogLevel.VERBOSE);
                        return true;
                    }
                    else
                    {
                        Log.Write("Starting to add new player to database.", LogLevel.VERBOSE);
                        AddPlayerToDatabase(guid);
                        return true;
                    }
                }
            }
            catch (MySqlException error)
            {
                Log.Write("Something went wrong when checking player's existence in database! Error code: " + error.Number + ": " + error.Message, LogLevel.ERROR);
                return false;
            }

        }

        public void AddPlayerToDatabase(long guid)
        {

            try
            {
                Log.Write("Adding player with GUID: " + guid + " to database.", LogLevel.DEBUG);
                using (var conn = new MySql.Data.MySqlClient.MySqlConnection())
                {
                    conn.ConnectionString = GlobalVariables.DbLocationAndDetails;
                    conn.Open();

                    MySqlCommand cmd = new MySqlCommand();
                    cmd.Connection = conn;

                    cmd.CommandText = "add_player";
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@player_guid", guid);
                    cmd.Parameters["@player_guid"].Direction = ParameterDirection.Input;

                    cmd.ExecuteNonQuery();

                    cmd.CommandText = "init_player_data";
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@guid", guid);
                    cmd.Parameters["@guid"].Direction = ParameterDirection.Input;

                    cmd.Parameters.AddWithValue("@currentScore", this._defaultScore);
                    cmd.Parameters["@currentScore"].Direction = ParameterDirection.Input;

                    cmd.ExecuteNonQuery();

                    conn.Close();
                }
            }
            catch (MySqlException error)
            {
                Log.Write(error.Number + ", " + error.Message, LogLevel.ERROR);
            }
        }

        public string GetStatsAsString()
        {
            try
            {
                if (IsNewPlayer())
                {
                    // This is a temporary fix to sync Discord bot and ingame values
                    Log.Write(this.Id + " is new player, returning default value.", LogLevel.VERBOSE);
                    return this._defaultScore + "," + this._defaultNumberOfTicks;
                }
                // Note: we need to add the square brackets in the calling code because of the status code being added
                Log.Write(this.Id + " is an old/existing player, total score: " + GetTotalScore() + ", ticks: " + GetTotalTicks(), LogLevel.VERBOSE);

                return GetTotalScore() + "," + GetTotalTicks();
            } catch (Exception error)
            {
                Log.Write(error.Message, LogLevel.ERROR);
            }

            return this._defaultScore + "," + this._defaultNumberOfTicks;
        }

        public void AddScore(long scoreDiff)
        {
            if (GetTotalTicks() >= this._maxNumberOfScores)
            {
                RemoveOldestScore();
                AddNewestScore(scoreDiff);
            }
            else
            {
                AddNewestScore(scoreDiff);
            }
        }

        private void AddNewestScore(long scoreDiff)
        {
            try
            {
                using (var conn = new MySql.Data.MySqlClient.MySqlConnection())
                {
                    conn.ConnectionString = GlobalVariables.DbLocationAndDetails;
                    conn.Open();

                    MySqlCommand cmd = new MySqlCommand();
                    cmd.Connection = conn;

                    cmd.CommandText = "add_player_score";
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@guid", this.Id);
                    cmd.Parameters["@guid"].Direction = ParameterDirection.Input;

                    cmd.Parameters.AddWithValue("@player_score", scoreDiff);
                    cmd.Parameters["@player_score"].Direction = ParameterDirection.Input;

                    cmd.Parameters.AddWithValue("@player_side", GetCurrentSide().ToString());
                    cmd.Parameters["@player_side"].Direction = ParameterDirection.Input;


                    cmd.ExecuteNonQuery();
                    conn.Close();
                }
            } catch (MySqlException error)
            {
                Log.Write(error.Number + ", " + error.Message, LogLevel.ERROR);
            }
        }

        private void RemoveOldestScore()
        {
            try
            {
                using (var conn = new MySql.Data.MySqlClient.MySqlConnection())
                {
                    conn.ConnectionString = GlobalVariables.DbLocationAndDetails;
                    conn.Open();

                    MySqlCommand cmd = new MySqlCommand();
                    cmd.Connection = conn;

                    cmd.CommandText = "remove_oldest_player_score";
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@guid", this.Id);
                    cmd.Parameters["@guid"].Direction = ParameterDirection.Input;

                    cmd.ExecuteNonQuery();
                    conn.Close();
                }
            } catch (MySqlException error)
            {
                Log.Write(error.Number + ", " + error.Message, LogLevel.ERROR);
            }
        }

        public double GetSkillAsDouble()
        {
            if (IsNewPlayer())
            {
                return Math.Round((double)this._defaultScore / this._defaultNumberOfTicks, 1);
            }
            else
            {
                return Math.Round(((double)GetTotalScore() / GetTotalTicks()), 1);
            };
        }

        public long GetTotalScore()
        {
            int totalScore = 1;

            try
            {
                using (var conn = new MySql.Data.MySqlClient.MySqlConnection())
                {
                    conn.ConnectionString = GlobalVariables.DbLocationAndDetails;
                    conn.Open();

                    MySqlCommand cmd = new MySqlCommand();
                    cmd.Connection = conn;

                    cmd.CommandText = "retrieve_player_total_score";
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@guid", this.Id);
                    cmd.Parameters["@guid"].Direction = ParameterDirection.Input;


                    cmd.Parameters.Add("@totalScore", MySqlDbType.Int32);
                    cmd.Parameters["@totalScore"].Direction = ParameterDirection.Output;

                    cmd.ExecuteNonQuery();

                    totalScore = Convert.ToInt32(cmd.Parameters["@totalScore"].Value);

                    conn.Close();
                }
            }
            catch (MySqlException error)
            {
                Log.Write(error.Number + ", " + error.Message, LogLevel.ERROR);
                totalScore = 1;
            }

            return totalScore;
        }

        public long GetTotalTicks()
        {
            int totalTicks = 1;
            try
            {
                using (var conn = new MySql.Data.MySqlClient.MySqlConnection())
                {
                    conn.ConnectionString = GlobalVariables.DbLocationAndDetails;
                    conn.Open();

                    MySqlCommand cmd = new MySqlCommand();
                    cmd.Connection = conn;

                    cmd.CommandText = "retrieve_player_total_ticks";
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@guid", this.Id);
                    cmd.Parameters["@guid"].Direction = ParameterDirection.Input;

                    cmd.Parameters.Add("@totalTicks", MySqlDbType.Int32);
                    cmd.Parameters["@totalTicks"].Direction = ParameterDirection.Output;

                    cmd.ExecuteNonQuery();

                    Log.Write("Player with GUID: " + this.Id + " total ticks: " + cmd.Parameters["@totalTicks"].Value, LogLevel.VERBOSE);

                    totalTicks = Convert.ToInt32(cmd.Parameters["@totalTicks"].Value);

                    conn.Close();
                }
            }
            catch (MySqlException error)
            {
                Log.Write(error.Number + ", " + error.ToString(), LogLevel.ERROR);
            }

            return totalTicks;
        }

        public bool StoreSide(long sideAsNumber)
        {

            string side = "";

            switch (sideAsNumber)
            {
                case 0:
                    side = Side.NONE.ToString();
                    StoreSideToDatabase(side);
                    this.Side = Side.NONE;
                    return true;
                case 1:
                    side = Side.WEST.ToString();
                    StoreSideToDatabase(side);
                    this.Side = Side.WEST;
                    return true;
                case 2:
                    side = Side.EAST.ToString();
                    StoreSideToDatabase(side);
                    this.Side = Side.EAST;
                    return true;
                default:
                    // Saving the player's side to database failed
                    return false;
            }
        }

        public void StoreSideToDatabase(string side)
        {
            try
            {
                using (var conn = new MySql.Data.MySqlClient.MySqlConnection())
                {
                    conn.ConnectionString = GlobalVariables.DbLocationAndDetails;
                    conn.Open();

                    MySqlCommand cmd = new MySqlCommand();
                    cmd.Connection = conn;

                    cmd.CommandText = "add_player_side";
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@guid", this.Id);
                    cmd.Parameters["@guid"].Direction = ParameterDirection.Input;

                    cmd.Parameters.AddWithValue("@playerSide", side);
                    cmd.Parameters["@playerSide"].Direction = ParameterDirection.Input;

                    cmd.ExecuteNonQuery();

                    conn.Close();
                }
            }
            catch (MySqlException error)
            {
                Log.Write(error.Number + ", :" + error.Message, LogLevel.ERROR);
            }
        }

        public bool IsNewPlayer()
        {
            if (GetTotalTicks() < this._ticksThresholdForCountingSkill)
            {
                return true;
            }

            return false;
        }

        internal Side GetCurrentSide()
        {
            return this.Side;
        }
    }

}
