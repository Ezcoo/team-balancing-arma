using Discord;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace A2WASPDiscordBot_Windows_App
{
    // Tracks threshold-ping messages so they can be deleted a fixed time after being sent,
    // persisted to disk so scheduled deletions survive a bot restart.
    public static class NotificationMessageCleanup
    {
        private static readonly TimeSpan MaxAge = TimeSpan.FromHours(6);
        private static readonly string FilePath = GlobalVariables.dataFolder + "PendingNotificationDeletions.json";
        private static readonly object FileLock = new object();

        private class Entry
        {
            public ulong ChannelId { get; set; }
            public ulong MessageId { get; set; }
            public DateTime SentAtUtc { get; set; }
        }

        public static void TrackForDeletion(ulong channelId, ulong messageId, DateTime sentAtUtc)
        {
            lock (FileLock)
            {
                var entries = LoadEntries();
                entries.Add(new Entry { ChannelId = channelId, MessageId = messageId, SentAtUtc = sentAtUtc });
                SaveEntries(entries);
            }
        }

        public static async Task SweepAsync()
        {
            List<Entry> entries;
            lock (FileLock)
            {
                entries = LoadEntries();
            }

            if (entries.Count == 0)
            {
                return;
            }

            DateTime cutoff = DateTime.UtcNow - MaxAge;
            var due = entries.FindAll(e => e.SentAtUtc <= cutoff);
            var remaining = entries.FindAll(e => e.SentAtUtc > cutoff);

            foreach (var entry in due)
            {
                try
                {
                    if (GlobalVariables.client.GetChannel(entry.ChannelId) is IMessageChannel channel)
                    {
                        await channel.DeleteMessageAsync(entry.MessageId);
                    }
                }
                catch (Exception ex)
                {
                    // Message may already be gone (manually deleted) - drop it from tracking regardless.
                    Log.Write($"Failed to delete expired notification message {entry.MessageId}: " + ex, LogLevel.ERROR);
                }
            }

            lock (FileLock)
            {
                SaveEntries(remaining);
            }
        }

        private static List<Entry> LoadEntries()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    return new List<Entry>();
                }

                string json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<List<Entry>>(json) ?? new List<Entry>();
            }
            catch (Exception ex)
            {
                Log.Write("Failed to load pending notification deletions; starting fresh. Error: " + ex, LogLevel.ERROR);
                return new List<Entry>();
            }
        }

        private static void SaveEntries(List<Entry> entries)
        {
            try
            {
                if (!Directory.Exists(GlobalVariables.dataFolder))
                {
                    Directory.CreateDirectory(GlobalVariables.dataFolder);
                }

                File.WriteAllText(FilePath, JsonSerializer.Serialize(entries));
            }
            catch (Exception ex)
            {
                Log.Write("Failed to save pending notification deletions: " + ex, LogLevel.ERROR);
            }
        }
    }
}
