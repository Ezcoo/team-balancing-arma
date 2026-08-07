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
    // "I want to play" gather feature: each user sets their own personal trigger count
    // ("notify me once N people want to play") and max wait time via a DM, remembered as
    // their default for next time. Joining is a one-click action in the channel using those
    // saved settings; a Settings button re-opens the DM to change them. State is persisted
    // so both profiles and the current interested list survive restarts.
    public static class PlayIntentButton
    {
        private const string CustomIdPrefix = "play_intent_";
        private const string JoinButtonId = CustomIdPrefix + "join";
        private const string SettingsButtonId = CustomIdPrefix + "settings";
        private const string LeaveButtonId = CustomIdPrefix + "leave";
        private const string ThresholdSelectId = CustomIdPrefix + "profile_threshold";
        private const string WaitSelectId = CustomIdPrefix + "profile_wait";

        private const int DefaultThreshold = 4;
        private const int DefaultWaitHours = 3;

        private static readonly int[] ThresholdOptions = { 2, 3, 4, 5, 6, 8, 10, 15, 20 };
        private static readonly int[] WaitHoursOptions = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        private static readonly string FilePath = GlobalVariables.dataFolder + "PlayIntent.json";
        private static readonly object FileLock = new object();

        private class Profile
        {
            public ulong UserId { get; set; }
            public int Threshold { get; set; }
            public int WaitHours { get; set; }
        }

        private class InterestEntry
        {
            public ulong UserId { get; set; }
            public DateTime ExpiresAtUtc { get; set; }
            public bool Notified { get; set; }
        }

        private class State
        {
            public List<Profile> Profiles { get; set; } = new List<Profile>();
            public List<InterestEntry> Interested { get; set; } = new List<InterestEntry>();
        }

        public static bool IsConfigured => GlobalVariables.PlayIntentEnabled;

        private const string PersistKey = "play_intent_menu";

        public static async Task EnsureMenuMessageAsync(IMessageChannel channel)
        {
            if (!IsConfigured)
            {
                Log.Write("playIntentEnabled is not set; skipping play-intent menu.", LogLevel.INFO);
                return;
            }

            int currentCount;
            lock (FileLock)
            {
                var state = LoadState();
                PruneExpired(state);
                currentCount = state.Interested.Count;
                SaveState(state);
            }

            var persisted = await PersistedMessageLookup.TryGetAsync(PersistKey, channel);
            if (persisted != null)
            {
                Log.Write($"Reusing persisted play-intent menu message (id={persisted.Id}).", LogLevel.INFO);
                await TryUpdateMenuContentAsync(persisted, currentCount);
                return;
            }

            try
            {
                var msgs = await channel.GetMessagesAsync(100).FlattenAsync();

                // Match specifically on our own button ids, so this doesn't collide with the
                // separate notify-threshold button menu message.
                var existing = msgs
                    .OfType<IUserMessage>()
                    .Where(m => m.Author != null && m.Author.Id == GlobalVariables.client.CurrentUser.Id
                        && m.Components.OfType<ActionRowComponent>().Any(row => row.Components.OfType<ButtonComponent>()
                            .Any(b => b.CustomId != null && b.CustomId.StartsWith(CustomIdPrefix))))
                    .OrderByDescending(m => m.Timestamp)
                    .FirstOrDefault();

                if (existing != null)
                {
                    Log.Write($"Reusing existing play-intent menu message (id={existing.Id}).", LogLevel.INFO);
                    PersistedMessageLookup.Save(PersistKey, channel.Id, existing.Id);
                    await TryUpdateMenuContentAsync(existing, currentCount);
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.Write("Failed to read channel history for play-intent menu; will send a new one. Error: " + ex, LogLevel.ERROR);
            }

            var components = new ComponentBuilder()
                .WithButton("🙋 I want to play!", JoinButtonId, ButtonStyle.Success)
                .WithButton("⚙️ Settings", SettingsButtonId, ButtonStyle.Secondary)
                .WithButton("❌ I don't want to play anymore (now)", LeaveButtonId, ButtonStyle.Danger);

            var sent = await channel.SendMessageAsync(BuildMenuContent(currentCount), components: components.Build());

            PersistedMessageLookup.Save(PersistKey, channel.Id, sent.Id);

            Log.Write("Sent new play-intent menu message.", LogLevel.INFO);
        }

        private static string BuildMenuContent(int currentCount)
        {
            return "------------------------------------------------------------------------\n**🙋 Looking to play?**\n\n" +
                $"Current number of players wanting to play: **{currentCount}**\n\n" +
                "Click **I want to play** to join the interested list using your saved settings (first time, I'll DM you to set them up).\n" +
                "Click **Settings** anytime to change how many players it takes to notify you and how long you're willing to wait. \nClick **I don't want to play anymore (now)** to drop off the list.\n";
        }

        private static async Task TryUpdateMenuContentAsync(IUserMessage message, int currentCount)
        {
            try
            {
                await message.ModifyAsync(m => m.Content = BuildMenuContent(currentCount));
            }
            catch (Exception ex)
            {
                Log.Write($"Failed to refresh play-intent menu message content (id={message.Id}): " + ex, LogLevel.ERROR);
            }
        }

        public static async Task HandleButtonInteractionAsync(SocketMessageComponent component)
        {
            string customId = component.Data.CustomId;
            if (customId != JoinButtonId && customId != SettingsButtonId && customId != LeaveButtonId)
            {
                return;
            }

            if (!(component.User is SocketGuildUser guildUser))
            {
                await component.RespondAsync("This can only be used in a server.", ephemeral: true);
                return;
            }

            if (customId == LeaveButtonId)
            {
                bool wasInterested;
                int currentCountAfterLeave;
                lock (FileLock)
                {
                    var state = LoadState();
                    PruneExpired(state);
                    wasInterested = state.Interested.RemoveAll(e => e.UserId == guildUser.Id) > 0;
                    currentCountAfterLeave = state.Interested.Count;
                    SaveState(state);
                }

                await component.RespondAsync(
                    wasInterested ? "You're no longer marked as wanting to play." : "You weren't marked as interested.",
                    ephemeral: true);

                if (wasInterested)
                {
                    await TryUpdateMenuContentAsync(component.Message, currentCountAfterLeave);
                }
                return;
            }

            if (customId == SettingsButtonId)
            {
                Profile profile;
                lock (FileLock)
                {
                    var state = LoadState();
                    profile = state.Profiles.FirstOrDefault(p => p.UserId == guildUser.Id)
                        ?? new Profile { UserId = guildUser.Id, Threshold = DefaultThreshold, WaitHours = DefaultWaitHours };
                }

                bool dmSent = await TrySendSettingsDmAsync(guildUser, profile, joinedNow: false);
                await component.RespondAsync(
                    dmSent ? "Check your DMs to update your settings!" : "I couldn't DM you - please enable DMs from server members and try again.",
                    ephemeral: true);
                return;
            }

            // Join.
            bool isFirstTime;
            Profile joinProfile;
            int currentCount;
            List<(ulong UserId, int Threshold)> newlyNotified;

            lock (FileLock)
            {
                var state = LoadState();
                PruneExpired(state);

                joinProfile = state.Profiles.FirstOrDefault(p => p.UserId == guildUser.Id);
                isFirstTime = joinProfile == null;
                if (joinProfile == null)
                {
                    joinProfile = new Profile { UserId = guildUser.Id, Threshold = DefaultThreshold, WaitHours = DefaultWaitHours };
                    state.Profiles.Add(joinProfile);
                }

                state.Interested.RemoveAll(e => e.UserId == guildUser.Id);
                state.Interested.Add(new InterestEntry
                {
                    UserId = guildUser.Id,
                    ExpiresAtUtc = DateTime.UtcNow + TimeSpan.FromHours(joinProfile.WaitHours),
                    Notified = false
                });

                newlyNotified = RecomputeNotifications(state);
                currentCount = state.Interested.Count;

                SaveState(state);
            }

            await component.RespondAsync(
                $"🙋 You're in! Waiting for **{joinProfile.Threshold}** interested ({currentCount} so far), up to **{joinProfile.WaitHours}h**.",
                ephemeral: true);

            await TryUpdateMenuContentAsync(component.Message, currentCount);

            if (isFirstTime)
            {
                await TrySendSettingsDmAsync(guildUser, joinProfile, joinedNow: true);
            }

            if (newlyNotified.Count > 0)
            {
                await SendGatherNotificationsAsync(currentCount, newlyNotified);
            }
        }

        public static async Task HandleSelectMenuInteractionAsync(SocketMessageComponent component)
        {
            string customId = component.Data.CustomId;
            if (customId != ThresholdSelectId && customId != WaitSelectId)
            {
                return;
            }

            string valueString = component.Data.Values.FirstOrDefault();
            if (!int.TryParse(valueString, out int value))
            {
                await component.RespondAsync("That option is no longer valid.", ephemeral: true);
                return;
            }

            ulong userId = component.User.Id;
            int savedThreshold;
            int savedWaitHours;
            bool isInterested;

            lock (FileLock)
            {
                var state = LoadState();
                PruneExpired(state);

                var profile = state.Profiles.FirstOrDefault(p => p.UserId == userId);
                if (profile == null)
                {
                    profile = new Profile { UserId = userId, Threshold = DefaultThreshold, WaitHours = DefaultWaitHours };
                    state.Profiles.Add(profile);
                }

                if (customId == ThresholdSelectId)
                {
                    profile.Threshold = value;
                }
                else
                {
                    profile.WaitHours = value;
                }

                savedThreshold = profile.Threshold;
                savedWaitHours = profile.WaitHours;

                var interestEntry = state.Interested.FirstOrDefault(e => e.UserId == userId);
                isInterested = interestEntry != null;
                if (interestEntry != null && customId == WaitSelectId)
                {
                    interestEntry.ExpiresAtUtc = DateTime.UtcNow + TimeSpan.FromHours(value);
                }

                SaveState(state);
            }

            string reply = customId == ThresholdSelectId
                ? $"Saved! You'll be notified once **{savedThreshold}** people want to play."
                : $"Saved! Your max wait time is now **{savedWaitHours}h**.";

            if (!isInterested)
            {
                reply += " Click **🙋 I want to play** in the server to join the interested list.";
            }

            await component.RespondAsync(reply, ephemeral: true);
        }

        private static List<(ulong UserId, int Threshold)> RecomputeNotifications(State state)
        {
            var result = new List<(ulong, int)>();
            int currentCount = state.Interested.Count;

            foreach (var entry in state.Interested)
            {
                if (entry.Notified)
                {
                    continue;
                }

                var profile = state.Profiles.FirstOrDefault(p => p.UserId == entry.UserId);
                int threshold = profile?.Threshold ?? DefaultThreshold;

                if (currentCount >= threshold)
                {
                    entry.Notified = true;
                    result.Add((entry.UserId, threshold));
                }
            }

            return result;
        }

        private static async Task<bool> TrySendSettingsDmAsync(IUser user, Profile profile, bool joinedNow)
        {
            try
            {
                var dmChannel = await user.CreateDMChannelAsync();

                await DeletePreviousSettingsMessagesAsync(dmChannel);

                var thresholdMenu = new SelectMenuBuilder()
                    .WithCustomId(ThresholdSelectId)
                    .WithPlaceholder($"Notify me once N people want to play (currently {profile.Threshold})")
                    .WithMinValues(1)
                    .WithMaxValues(1);
                foreach (var count in ThresholdOptions)
                {
                    thresholdMenu.AddOption($"{count} people", count.ToString(), isDefault: count == profile.Threshold);
                }

                var waitMenu = new SelectMenuBuilder()
                    .WithCustomId(WaitSelectId)
                    .WithPlaceholder($"Max wait time (currently {profile.WaitHours}h)")
                    .WithMinValues(1)
                    .WithMaxValues(1);
                foreach (var hours in WaitHoursOptions)
                {
                    waitMenu.AddOption($"Wait up to {hours} hour{(hours == 1 ? "" : "s")}", hours.ToString(), isDefault: hours == profile.WaitHours);
                }

                var components = new ComponentBuilder()
                    .WithSelectMenu(thresholdMenu, row: 0)
                    .WithSelectMenu(waitMenu, row: 1);

                string intro = joinedNow
                    ? $"🙋 You're on the interested list with default settings: notify you once **{profile.Threshold}** people want to play, waiting up to **{profile.WaitHours}h**."
                    : "Update your \"I want to play\" settings below:";

                await dmChannel.SendMessageAsync(
                    intro + "\n\nUse the dropdowns to change how many players it takes to notify you, and your maximum wait time.",
                    components: components.Build());

                return true;
            }
            catch (Exception ex)
            {
                Log.Write($"Failed to DM play-intent settings to user {user.Id}: " + ex, LogLevel.ERROR);
                return false;
            }
        }

        private static async Task DeletePreviousSettingsMessagesAsync(IDMChannel dmChannel)
        {
            try
            {
                var msgs = await dmChannel.GetMessagesAsync(50).FlattenAsync();

                var stale = msgs
                    .OfType<IUserMessage>()
                    .Where(m => m.Author != null && m.Author.Id == GlobalVariables.client.CurrentUser.Id
                        && m.Components.OfType<ActionRowComponent>().Any(row => row.Components.OfType<SelectMenuComponent>()
                            .Any(s => s.CustomId == ThresholdSelectId || s.CustomId == WaitSelectId)));

                foreach (var message in stale)
                {
                    try
                    {
                        await message.DeleteAsync();
                    }
                    catch (Exception ex)
                    {
                        Log.Write($"Failed to delete stale play-intent settings message {message.Id}: " + ex, LogLevel.ERROR);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write("Failed to read DM history to clean up stale settings messages: " + ex, LogLevel.ERROR);
            }
        }

        private static async Task SendGatherNotificationsAsync(int currentCount, List<(ulong UserId, int Threshold)> newlyNotified)
        {
            foreach (var (userId, threshold) in newlyNotified)
            {
                try
                {
                    var user = await GlobalVariables.client.Rest.GetUserAsync(userId);
                    if (user != null)
                    {
                        var dmChannel = await user.CreateDMChannelAsync();
                        var sentMessage = await dmChannel.SendMessageAsync(
                            $"--------------------------------------------------------------------------------------------- \n" +
                            $"🙋 **{currentCount}** people want to play now! **It's time to join the Miksuu's Warfare server in Arma 2! :)** \n" +
                            $"---------------------------------------------------------------------------------------------");

                        NotificationMessageCleanup.TrackForDeletion(dmChannel.Id, sentMessage.Id, TimeSpan.FromHours(24));
                    }
                }
                catch (Exception ex)
                {
                    Log.Write($"Failed to DM user {userId} about gather notification (they may have DMs disabled): " + ex, LogLevel.ERROR);
                }
            }
        }

        private static void PruneExpired(State state)
        {
            state.Interested.RemoveAll(e => e.ExpiresAtUtc <= DateTime.UtcNow);
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
