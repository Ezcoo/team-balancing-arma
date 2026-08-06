using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace A2WASPDiscordBot_Windows_App
{
    // "I want to play" gather feature: users pick how long they're willing to wait via a
    // dropdown, and once enough people are interested at once, everyone currently interested
    // gets pinged (a low-key channel heads-up plus an individual DM). Interest expires after
    // the user's chosen wait time, and state is persisted so it survives restarts.
    public static class PlayIntentButton
    {
        private const string SelectMenuId = "play_intent_select";
        private const string LeaveValue = "leave";
        private static readonly int[] WaitHoursOptions = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        private static readonly string FilePath = GlobalVariables.dataFolder + "PlayIntent.json";
        private static readonly object FileLock = new object();

        private class Entry
        {
            public ulong UserId { get; set; }
            public DateTime ExpiresAtUtc { get; set; }
        }

        private class State
        {
            public List<Entry> Interested { get; set; } = new List<Entry>();
            public List<int> FiredThresholds { get; set; } = new List<int>();
        }

        public static bool IsConfigured => GlobalVariables.PlayIntentThresholds.Count > 0;

        public static void AddSelectMenu(ComponentBuilder components)
        {
            if (!IsConfigured)
            {
                return;
            }

            var menu = new SelectMenuBuilder()
                .WithCustomId(SelectMenuId)
                .WithPlaceholder("🙋 I want to play - pick your max wait time")
                .WithMinValues(1)
                .WithMaxValues(1);

            foreach (var hours in WaitHoursOptions)
            {
                menu.AddOption($"Wait up to {hours} hour{(hours == 1 ? "" : "s")}", hours.ToString());
            }

            menu.AddOption("❌ I'm no longer interested", LeaveValue);

            components.WithSelectMenu(menu, row: 1);
        }

        public static async Task HandleSelectMenuInteractionAsync(SocketMessageComponent component)
        {
            if (component.Data.CustomId != SelectMenuId)
            {
                return;
            }

            if (!(component.User is SocketGuildUser guildUser))
            {
                await component.RespondAsync("This can only be used in a server.", ephemeral: true);
                return;
            }

            string value = component.Data.Values.FirstOrDefault();
            bool leaving = value == LeaveValue;
            if (!leaving && !int.TryParse(value, out int parsedWaitHours))
            {
                await component.RespondAsync("That option is no longer valid.", ephemeral: true);
                return;
            }
            int waitHours = leaving ? 0 : int.Parse(value);

            bool wasInterested;
            int currentCount;
            List<ulong> interestedUserIds;
            int? tierToFire = null;

            lock (FileLock)
            {
                var state = LoadState();
                state.Interested.RemoveAll(e => e.ExpiresAtUtc <= DateTime.UtcNow);

                wasInterested = state.Interested.Any(e => e.UserId == guildUser.Id);
                state.Interested.RemoveAll(e => e.UserId == guildUser.Id);

                if (!leaving)
                {
                    state.Interested.Add(new Entry { UserId = guildUser.Id, ExpiresAtUtc = DateTime.UtcNow + TimeSpan.FromHours(waitHours) });
                }

                currentCount = state.Interested.Count;
                interestedUserIds = state.Interested.Select(e => e.UserId).ToList();

                // Only a fresh join can cross a milestone upward - leaving, or just refreshing
                // an already-interested user's wait time, shouldn't trigger a ping.
                if (!leaving && !wasInterested)
                {
                    var thresholds = GlobalVariables.PlayIntentThresholds;
                    foreach (var threshold in thresholds)
                    {
                        if (currentCount >= threshold && !state.FiredThresholds.Contains(threshold))
                        {
                            tierToFire = threshold;
                            state.FiredThresholds.Add(threshold);
                        }
                    }

                    int highestConfigured = thresholds.Count > 0 ? thresholds[thresholds.Count - 1] : 0;
                    if (tierToFire.HasValue && tierToFire.Value == highestConfigured)
                    {
                        // Full round complete - reset so the next round starts from zero.
                        state.Interested.Clear();
                        state.FiredThresholds.Clear();
                    }
                }

                if (currentCount == 0)
                {
                    state.FiredThresholds.Clear();
                }

                SaveState(state);
            }

            string replyMessage;
            if (leaving)
            {
                replyMessage = wasInterested
                    ? "You're no longer marked as wanting to play."
                    : "You weren't marked as interested.";
            }
            else
            {
                replyMessage = $"🙋 You're in for up to **{waitHours}h**! ({currentCount} interested right now)";
            }

            await component.RespondAsync(replyMessage, ephemeral: true);

            if (tierToFire.HasValue && interestedUserIds.Count > 0)
            {
                var channel = ResolveNotifyChannel(component.Channel as IMessageChannel);
                await SendGatherPingAsync(channel, tierToFire.Value, interestedUserIds);
            }
        }

        private static IMessageChannel ResolveNotifyChannel(IMessageChannel fallback)
        {
            ulong notifyChannelId = GlobalVariables.ConvertIDtoULong(GlobalVariables.NotifyChannel1);
            if (notifyChannelId != 0 && GlobalVariables.client.GetChannel(notifyChannelId) is IMessageChannel resolved)
            {
                return resolved;
            }

            return fallback;
        }

        private static async Task SendGatherPingAsync(IMessageChannel channel, int tier, List<ulong> userIds)
        {
            try
            {
                if (channel != null)
                {
                    await channel.SendMessageAsync($"🙋 **{tier}+ people want to play!** Check your DMs.");
                }
            }
            catch (Exception ex)
            {
                Log.Write("Failed to send play-intent channel heads-up: " + ex, LogLevel.ERROR);
            }

            foreach (var userId in userIds)
            {
                try
                {
                    var user = await GlobalVariables.client.Rest.GetUserAsync(userId);
                    if (user != null)
                    {
                        var dmChannel = await user.CreateDMChannelAsync();
                        await dmChannel.SendMessageAsync($"🙋 **{tier}+ people want to play!** Hop in if you're still up for it.");
                    }
                }
                catch (Exception ex)
                {
                    Log.Write($"Failed to DM user {userId} about gather ping (they may have DMs disabled): " + ex, LogLevel.ERROR);
                }
            }
        }

        private static State LoadState()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    return new State();
                }

                string json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<State>(json) ?? new State();
            }
            catch (Exception ex)
            {
                Log.Write("Failed to load play intent state; starting fresh. Error: " + ex, LogLevel.ERROR);
                return new State();
            }
        }

        private static void SaveState(State state)
        {
            try
            {
                if (!Directory.Exists(GlobalVariables.dataFolder))
                {
                    Directory.CreateDirectory(GlobalVariables.dataFolder);
                }

                File.WriteAllText(FilePath, JsonSerializer.Serialize(state));
            }
            catch (Exception ex)
            {
                Log.Write("Failed to save play intent state: " + ex, LogLevel.ERROR);
            }
        }
    }
}
