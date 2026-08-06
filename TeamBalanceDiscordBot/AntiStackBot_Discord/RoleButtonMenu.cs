using Discord;
using Discord.Net;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace A2WASPDiscordBot_Windows_App
{
    public static class RoleButtonMenu
    {
        private const string CustomIdPrefix = "notify_role:";

        public static async Task EnsureButtonMessageAsync(IMessageChannel channel)
        {
            if (GlobalVariables.NotifyRoleThresholds.Count == 0)
            {
                Log.Write("No notifyRoleThresholds configured; skipping notification button menu.", LogLevel.INFO);
                return;
            }

            try
            {
                var msgs = await channel.GetMessagesAsync(100).FlattenAsync();

                // Match specifically on our own button id prefix, so this doesn't collide with
                // the separate play-intent menu message (which also uses buttons now).
                var existing = msgs
                    .OfType<IUserMessage>()
                    .Where(m => m.Author != null && m.Author.Id == GlobalVariables.client.CurrentUser.Id
                        && m.Components.OfType<ActionRowComponent>().Any(row => row.Components.OfType<ButtonComponent>()
                            .Any(b => b.CustomId != null && b.CustomId.StartsWith(CustomIdPrefix))))
                    .OrderByDescending(m => m.Timestamp)
                    .FirstOrDefault();

                if (existing != null)
                {
                    Log.Write($"Reusing existing notification button message (id={existing.Id}).", LogLevel.INFO);
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.Write("Failed to read channel history for button menu; will send a new one. Error: " + ex, LogLevel.ERROR);
            }

            var components = new ComponentBuilder();
            foreach (var threshold in GlobalVariables.NotifyRoleThresholds.Keys.OrderBy(t => t))
            {
                components.WithButton($"{threshold}+", $"{CustomIdPrefix}{threshold}", ButtonStyle.Success);
            }

            await channel.SendMessageAsync(
                "**🔔 Player Count Notifications**\nClick a button to toggle a role that pings you when the player count reaches that many players.",
                components: components.Build());

            Log.Write("Sent new notification button menu message.", LogLevel.INFO);
        }

        public static async Task HandleButtonInteractionAsync(SocketMessageComponent component)
        {
            if (component.Data.CustomId == null || !component.Data.CustomId.StartsWith(CustomIdPrefix))
            {
                return;
            }

            string thresholdString = component.Data.CustomId.Substring(CustomIdPrefix.Length);
            if (!int.TryParse(thresholdString, out int threshold) || !GlobalVariables.NotifyRoleThresholds.TryGetValue(threshold, out ulong roleId))
            {
                await component.RespondAsync("This notification role is no longer configured.", ephemeral: true);
                return;
            }

            if (!(component.User is SocketGuildUser guildUser))
            {
                await component.RespondAsync("This can only be used in a server.", ephemeral: true);
                return;
            }

            try
            {
                if (guildUser.Roles.Any(r => r.Id == roleId))
                {
                    await guildUser.RemoveRoleAsync(roleId);
                    await component.RespondAsync($"🔕 You will no longer be pinged at **{threshold}+** players.", ephemeral: true);
                }
                else
                {
                    await guildUser.AddRoleAsync(roleId);
                    await component.RespondAsync($"🔔 You will be pinged at **{threshold}+** players.", ephemeral: true);
                }
            }
            catch (HttpException ex)
            {
                Log.Write($"Failed to toggle role {roleId} for user {guildUser.Id}: " + ex, LogLevel.ERROR);
                await component.RespondAsync("Sorry, I couldn't update your roles. Please contact an admin (I may be missing permissions or role hierarchy).", ephemeral: true);
            }
        }
    }
}
