using Discord;
using Discord.WebSocket;
using Discord.Rest;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using MySql.Data.MySqlClient;
using System.Data.Common;

namespace A2WASPDiscordBot_Windows_App
{
    public class GlobalVariables
    {
        public const ulong GuildID = 1117736812270600332;

        public static readonly string dbConnectionString = @"server=localhost;uid=timppa;pwd=5ohG6vDWHM7mZvtAcV64aVAG9;database=antistack";
        private static readonly String dbConnectionStringFinal = Environment.ExpandEnvironmentVariables(dbConnectionString);

        public static readonly string logsFolder = @"\Logs\";

        public static DiscordSocketConfig discordSocketconfig = new DiscordSocketConfig()
        {
            GatewayIntents = GatewayIntents.AllUnprivileged,
        };

        public static readonly DiscordSocketClient client = new DiscordSocketClient(discordSocketconfig);

        /*
        Related procedures:

        private static readonly string flushSideOfAllPlayers = @"CREATE PROCEDURE get_active_players_count_side(IN requestedSide VARCHAR(30), OUT playerCount INT) SELECT COUNT(players) INTO playerCount WHERE side=requestedSide";

        */

    }
}