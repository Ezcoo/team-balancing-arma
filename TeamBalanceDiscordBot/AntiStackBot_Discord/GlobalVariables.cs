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

        static string GuildID = Environment.GetEnvironmentVariable("armaDiscordBotGuildID");
        static string uidEnvVariable = Environment.GetEnvironmentVariable("databaseUid");
        static string passwordEnvVariable = Environment.GetEnvironmentVariable("databasePassword");
        static string databaseEnvVariable = Environment.GetEnvironmentVariable("databaseName");
        static string GuildChannel = Environment.GetEnvironmentVariable("guildChannel");
        static string BotToken = Environment.GetEnvironmentVariable("discordBotToken");

        public static readonly string dbConnectionString = @"server=localhost;uid=" + uidEnvVariable + ";pwd=" + passwordEnvVariable + ";database=" + databaseEnvVariable;

        public static readonly string logsFolder = @"\Logs\";

        public static DiscordSocketConfig discordSocketconfig = new DiscordSocketConfig()
        {
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages
        };

        public static readonly DiscordSocketClient client = new DiscordSocketClient(discordSocketconfig);

        public static string GuildID1 { get => GuildID; }
        public static string GuildChannel1 { get => GuildChannel; }
        public static string BotToken1 { get => BotToken; }

        public static ulong ConvertIDtoULong(string ulongString)
        {
            if (ulong.TryParse(ulongString, out ulong value))
            {
                return value;
            }
            else
            {
                return 0;
            }

        }

        /*
        Related procedures:

        private static readonly string flushSideOfAllPlayers = @"CREATE PROCEDURE get_active_players_count_side(IN requestedSide VARCHAR(30), OUT playerCount INT) SELECT COUNT(players) INTO playerCount WHERE side=requestedSide";

        */

    }
}
