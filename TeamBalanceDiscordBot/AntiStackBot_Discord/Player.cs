
using System.Collections.Generic;
using System;
using MySql.Data.MySqlClient;
using System.Data;

namespace A2WASPDiscordBot_Windows_App
{
    public class Player
    {
        public long id { get; set; }
        public List<long> scoresList { get; set; }
        public Side side { get; set; }
        private long maxNumberOfScores;
        private long ticksThresholdForCountingSkill;
        private long defaultNumberOfTicks;
        private int defaultScore;
        private long currentTicksCached;

        public Player(long id)
        {
            Log.Write("Initializing new (?) player with GUID: " + id, LogLevel.DEBUG);
            this.id = id;
            this.scoresList = new List<long>();
            this.maxNumberOfScores = 500;
            this.ticksThresholdForCountingSkill = 40;
            this.defaultNumberOfTicks = 10;
            this.defaultScore = 120;
            this.side = Side.NONE;
            this.currentTicksCached = this.defaultNumberOfTicks;
            existsInDatabase(this.id);
            Log.Write("Exiting Player constructor method (GUID: " + id + ")", LogLevel.DEBUG);
        }

        public Boolean existsInDatabase(long guid)
        {
            try
            {
                using (var conn = new MySql.Data.MySqlClient.MySqlConnection())
                {
                    conn.ConnectionString = GlobalVariables.dbConnectionString;
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
                    catch (MySqlException e)
                    {
                        Log.Write(e.Number + ", " + e.Message, LogLevel.ERROR);
                        boolAsInt = 0;
                        conn.Close();
                        return false;
                    }

                    if (boolAsInt > 0)
                    {
                        Log.Write("Player with GUID: " + this.id + " was found from the database.", LogLevel.DEBUG);
                        return true;
                    }
                    else
                    {
                        Log.Write("Player with GUID: " + this.id + " was not found from the database!", LogLevel.WARNING);
                        return false;
                    }
                }
            }
            catch (MySqlException e)
            {
                Log.Write("Something went wrong when checking player's existence in database! Error code: " + e.Number + ": " + e.Message, LogLevel.ERROR);
                return false;
            }

        }

        public double GetSkillAsDouble()
        {
            if (IsNewPlayer())
            {
                return Math.Round((double)this.defaultScore / this.defaultNumberOfTicks, 1);
            }
            else
            {
                // IsNewPlayer() call above calls GetTotalTicks() internally, which caches the current total ticks
                // to prevent exceeding pooled connection limits in database and as an optimization in general
                // ...so if the cached ticks number has changed from default value, we can use the cached value
                if (this.currentTicksCached != this.defaultNumberOfTicks)
                {
                    return Math.Round(((double)GetTotalScore() / this.currentTicksCached), 1);
                }
                else
                {
                    return Math.Round(((double)GetTotalScore() / GetTotalTicks()), 1);
                }
            }
        }

        public long GetTotalScore()
        {
            int totalScore = 1;

            try
            {
                using (var conn = new MySql.Data.MySqlClient.MySqlConnection())
                {
                    conn.ConnectionString = GlobalVariables.dbConnectionString;
                    conn.Open();

                    MySqlCommand cmd = new MySqlCommand();
                    cmd.Connection = conn;

                    cmd.CommandText = "retrieve_player_total_score";
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@guid", this.id);
                    cmd.Parameters["@guid"].Direction = ParameterDirection.Input;


                    cmd.Parameters.Add("@totalScore", MySqlDbType.Int32);
                    cmd.Parameters["@totalScore"].Direction = ParameterDirection.Output;

                    cmd.ExecuteNonQuery();

                    totalScore = Convert.ToInt32(cmd.Parameters["@totalScore"].Value);

                    conn.Close();
                }
            }
            catch (MySqlException e)
            {
                Log.Write(e.Number + ", " + e.Message, LogLevel.ERROR);
                totalScore = 1;
            }

            return totalScore;
        }

        public long GetTotalTicks()
        {
            long totalTicks = this.defaultNumberOfTicks;
            try
            {
                using (var conn = new MySql.Data.MySqlClient.MySqlConnection())
                {
                    conn.ConnectionString = GlobalVariables.dbConnectionString;
                    conn.Open();

                    MySqlCommand cmd = new MySqlCommand();
                    cmd.Connection = conn;

                    cmd.CommandText = "retrieve_player_total_ticks";
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@guid", this.id);
                    cmd.Parameters["@guid"].Direction = ParameterDirection.Input;

                    cmd.Parameters.Add("@totalTicks", MySqlDbType.Int32);
                    cmd.Parameters["@totalTicks"].Direction = ParameterDirection.Output;

                    cmd.ExecuteNonQuery();

                    // Log.Write("TotalTicks: " + cmd.Parameters["@totalTicks"].Value, LogLevel.DEBUG);

                    totalTicks = Convert.ToInt32(cmd.Parameters["@totalTicks"].Value);

                    conn.Close();
                }
            }
            catch (MySqlException e)
            {
                Log.Write(e.Number + ", " + e.ToString(), LogLevel.ERROR);
            }

            this.currentTicksCached = totalTicks;
            return totalTicks;
        }

        public bool IsNewPlayer()
        {
            if (GetTotalTicks() < this.ticksThresholdForCountingSkill)
            {
                return true;
            }

            return false;
        }

        internal Side GetCurrentSide()
        {
            return this.side;
        }
    }
}