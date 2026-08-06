using Discord;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace A2WASPDiscordBot_Windows_App
{
    // Persists a single (channelId, messageId) pair per named message-slot to disk, so that
    // slot's message can be found directly by id after a restart instead of relying purely on
    // searching recent channel history - which can miss it if enough other messages were
    // posted in between, causing a duplicate (new one sent) or an orphaned old one.
    public static class PersistedMessageLookup
    {
        private class Reference
        {
            public ulong ChannelId { get; set; }
            public ulong MessageId { get; set; }
        }

        public static async Task<IUserMessage> TryGetAsync(string key, IMessageChannel channel)
        {
            var reference = Load(key);
            if (reference == null || reference.ChannelId != channel.Id)
            {
                return null;
            }

            try
            {
                return await channel.GetMessageAsync(reference.MessageId) as IUserMessage;
            }
            catch (Exception ex)
            {
                Log.Write($"Failed to fetch persisted message for '{key}' (id={reference.MessageId}), it may have been deleted: " + ex, LogLevel.ERROR);
                return null;
            }
        }

        public static void Save(string key, ulong channelId, ulong messageId)
        {
            try
            {
                if (!Directory.Exists(GlobalVariables.dataFolder))
                {
                    Directory.CreateDirectory(GlobalVariables.dataFolder);
                }

                File.WriteAllText(FilePath(key), JsonSerializer.Serialize(new Reference { ChannelId = channelId, MessageId = messageId }));
            }
            catch (Exception ex)
            {
                Log.Write($"Failed to persist message reference for '{key}': " + ex, LogLevel.ERROR);
            }
        }

        private static Reference Load(string key)
        {
            try
            {
                string path = FilePath(key);
                if (!File.Exists(path))
                {
                    return null;
                }

                return JsonSerializer.Deserialize<Reference>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                Log.Write($"Failed to load message reference for '{key}': " + ex, LogLevel.ERROR);
                return null;
            }
        }

        private static string FilePath(string key) => GlobalVariables.dataFolder + $"Message_{key}.json";
    }
}
