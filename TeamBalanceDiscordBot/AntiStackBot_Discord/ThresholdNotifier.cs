using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace A2WASPDiscordBot_Windows_App
{
    public static class ThresholdNotifier
    {
        private static readonly TimeSpan Cooldown = TimeSpan.FromHours(2);
        private static readonly Dictionary<int, DateTime> _lastPingUtc = new Dictionary<int, DateTime>();

        public static async Task CheckAndNotifyAsync(IMessageChannel channel, int currentPlayerCount)
        {
            foreach (var entry in GlobalVariables.NotifyRoleThresholds.OrderBy(e => e.Key))
            {
                int threshold = entry.Key;
                ulong roleId = entry.Value;

                if (currentPlayerCount < threshold)
                {
                    continue;
                }

                if (_lastPingUtc.TryGetValue(threshold, out DateTime lastPing) && (DateTime.UtcNow - lastPing) < Cooldown)
                {
                    continue;
                }

                try
                {
                    var sentMessage = await channel.SendMessageAsync(
                        $"<@&{roleId}> Player count on Miksuu's Warfare server in Arma 2 has reached the threshold you have set: Currently, there are **{currentPlayerCount}** players ingame! **Welcome to the server! :)**",
                        allowedMentions: new AllowedMentions { RoleIds = new List<ulong> { roleId } });

                    _lastPingUtc[threshold] = DateTime.UtcNow;

                    NotificationMessageCleanup.TrackForDeletion(channel.Id, sentMessage.Id, TimeSpan.FromHours(6));
                }
                catch (Exception ex)
                {
                    Log.Write($"Failed to send threshold ping for {threshold}+ (role {roleId}): " + ex, LogLevel.ERROR);
                }
            }
        }
    }
}
