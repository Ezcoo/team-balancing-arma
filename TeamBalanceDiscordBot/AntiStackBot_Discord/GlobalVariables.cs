using Discord;
using Discord.WebSocket;
using Discord.Rest;
using System;
using System.Collections.Generic;
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
        static string NotifyRoleThresholdsRaw = Environment.GetEnvironmentVariable("notifyRoleThresholds");
        static string NotifyChannel = Environment.GetEnvironmentVariable("notifyChannel");
        static string PlayIntentEnabledRaw = Environment.GetEnvironmentVariable("playIntentEnabled");

        public static readonly string dbConnectionString = @"server=localhost;uid=" + uidEnvVariable + ";pwd=" + passwordEnvVariable + ";database=" + databaseEnvVariable;

        // Format: "threshold:roleId,threshold:roleId,..." e.g. "5:111...,10:222...,15:333...,20:444..."
        public static readonly Dictionary<int, ulong> NotifyRoleThresholds = ParseNotifyRoleThresholds(NotifyRoleThresholdsRaw);

        // Set to "true" or "1" to enable the "I want to play" gather feature.
        public static readonly bool PlayIntentEnabled = string.Equals(PlayIntentEnabledRaw, "true", StringComparison.OrdinalIgnoreCase) || PlayIntentEnabledRaw == "1";

        public static readonly string logsFolder = @"\Logs\";
        public static readonly string dataFolder = @"\Data\";

        public static DiscordSocketConfig discordSocketconfig = new DiscordSocketConfig()
        {
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages
        };

        public static readonly DiscordSocketClient client = new DiscordSocketClient(discordSocketconfig);

        public static string GuildID1 { get => GuildID; }
        public static string GuildChannel1 { get => GuildChannel; }
        public static string BotToken1 { get => BotToken; }
        public static string NotifyChannel1 { get => NotifyChannel; }

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

        private static Dictionary<int, ulong> ParseNotifyRoleThresholds(string raw)
        {
            var result = new Dictionary<int, ulong>();

            if (string.IsNullOrWhiteSpace(raw))
            {
                return result;
            }

            foreach (var pair in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out int threshold) && ulong.TryParse(parts[1], out ulong roleId))
                {
                    result[threshold] = roleId;
                }
            }

            return result;
        }

        /*
        Related procedures:

        private static readonly string flushSideOfAllPlayers = @"CREATE PROCEDURE get_active_players_count_side(IN requestedSide VARCHAR(30), OUT playerCount INT) SELECT COUNT(players) INTO playerCount WHERE side=requestedSide";

        */

    }
}
