using AntiStackBot_Discord;
using Discord;
using Discord.WebSocket;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Discord.Net;

namespace A2WASPDiscordBot_Windows_App
{
    public class Program
    {
        private static DiscordSocketClient _client;
        private static EmbedBuilder _embedBuilder;

        public static Task Main(string[] args) => new Program().MainAsync();

        public async Task MainAsync()
        {
            _embedBuilder = new EmbedBuilder
            {
                // Embed property can be set within object initializer
                Title = "",
                Description = ""
            };

            _client = GlobalVariables.client;

            LoggingService loggingService = new LoggingService(_client);

            //  You can assign your bot token to a string, and pass that in to connect.
            //  This is, however, insecure, particularly if you plan to have your code hosted in a public repository.

            // Some alternative options would be to keep your token in an Environment Variable or a standalone file.
            string token = GlobalVariables.BotToken1;

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            // _client.MessageUpdated += MessageUpdated;

            Log.Write("Initializing the bot...", LogLevel.INFO);

            _client.Ready += OnReadyAsync;

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        private static Task OnReadyAsync()
        {
            Log.Write("Bot is ready!", LogLevel.IMPORTANT);

            // Run the long loop in the background so the gateway thread is never blocked.
            _ = Task.Run(async () =>
            {
                try
                {
                    await RunStatusLoopAsync();
                }
                catch (Exception ex)
                {
                    Log.Write("Status loop crashed: " + ex, LogLevel.ERROR);
                }
            });

            return Task.CompletedTask;
        }

        private static async Task RunStatusLoopAsync()
        {
            const int UpdateIntervalMs = 5100;
            const int HistorySearchLimit = 100;

            await Task.Delay(UpdateIntervalMs); // small initial delay, optional

            ulong channelId = GlobalVariables.ConvertIDtoULong(GlobalVariables.GuildChannel1);
            Log.Write($"Looking up channel id: {channelId}", LogLevel.INFO);

            var channel = _client.GetChannel(channelId) as IMessageChannel;
            if (channel == null)
            {
                Log.Write("Channel not found / not IMessageChannel (check GuildChannel1 id).", LogLevel.ERROR);
                return;
            }

            // Create (only if needed) OR reuse the latest bot-authored message in that channel.
            IUserMessage statusMessage = null;

            while (true)
            {
                try
                {
                    // If we don't have a message to edit yet (or it was deleted), try to find one. If none exists, send a new one.
                    if (statusMessage == null)
                    {
                        statusMessage = await GetOrCreateStatusMessageAsync(channel, HistorySearchLimit);
                    }

                    _embedBuilder
                        .WithFooter(footer => footer.Text = "Thank you for balancing the teams! Good luck – and have fun! :)")
                        .WithTitle("TEAM SKILL BALANCE")
                        .WithDescription(GetMessage())
                        .WithCurrentTimestamp();

                    await statusMessage.ModifyAsync(m => m.Embed = _embedBuilder.Build());

                    await Task.Delay(UpdateIntervalMs);
                }
                catch (HttpException httpEx)
                {
                    // Common cases:
                    // - Missing Access / Missing Permissions
                    // - Unknown Message (message deleted) -> reacquire / re-send
                    Log.Write("Update failed (HTTP): " + httpEx, LogLevel.ERROR);

                    // If the message was deleted, Discord usually returns "Unknown Message" (10008).
                    // In that case, clear the cached message so we'll search/send again.
                    if (httpEx.DiscordCode == DiscordErrorCode.UnknownMessage)
                    {
                        statusMessage = null;
                    }

                    await Task.Delay(5000); // backoff before retry
                }
                catch (Exception ex)
                {
                    Log.Write("Update failed: " + ex, LogLevel.ERROR);
                    await Task.Delay(5000); // backoff before retry
                }
            }
        }

        /// <summary>
        /// Returns the latest message in the channel authored by this bot. If none exists, sends a new one and returns it.
        /// This ensures we only send a new message when there are no other bot messages in the channel.
        /// </summary>
        private static async Task<IUserMessage> GetOrCreateStatusMessageAsync(IMessageChannel channel, int historyLimit)
        {
            try
            {
                var msgs = await channel.GetMessagesAsync(historyLimit).FlattenAsync();

                // Pick the newest message authored by THIS bot in the channel.
                var existing = msgs
                    .OfType<IUserMessage>()
                    .Where(m => m.Author != null && m.Author.Id == _client.CurrentUser.Id)
                    .OrderByDescending(m => m.Timestamp)
                    .FirstOrDefault();

                if (existing != null)
                {
                    Log.Write($"Reusing existing bot message (id={existing.Id}) in channel.", LogLevel.INFO);
                    return existing;
                }
            }
            catch (Exception ex)
            {
                // If we cannot read history (missing permission), fall back to sending a new message.
                Log.Write("Failed to read channel history; will send a new status message. Error: " + ex, LogLevel.ERROR);
            }

            _embedBuilder
                .WithFooter(footer => footer.Text = "Thank you for balancing the teams! Good luck – and have fun! :)")
                .WithTitle("TEAM SKILL BALANCE")
                .WithDescription(GetMessage())
                .WithCurrentTimestamp();

            Log.Write("No existing bot messages found; sending a new status message.", LogLevel.INFO);
            return await channel.SendMessageAsync(" ", embed: _embedBuilder.Build());
        }




        private static async Task MessageCreate()
        {
            Thread.Sleep(5100);

            // Add your channel here
            IMessageChannel channel = _client.GetChannel(GlobalVariables.ConvertIDtoULong(GlobalVariables.GuildChannel1)) as IMessageChannel;
            string messageString = " ";

            _embedBuilder
                .WithFooter(footer => footer.Text = "Thank you for balancing the teams! Good luck – and have fun! :) \n\nThis status was updated: ")
                .WithTitle("TEAM SKILL BALANCE")
                .WithDescription(GetMessage())
                .WithCurrentTimestamp();

            // Send a message 'message' to channel 'channel' here
            Task<IUserMessage> messageEmbed = channel.SendMessageAsync(messageString, false, _embedBuilder.Build());

            IUserMessage result = messageEmbed.Result;

            while (true)
            {
                await MessageUpdate(result);
            }
        }

        private static async Task MessageUpdate(IUserMessage originalMessage)
        {
            Thread.Sleep(6100);

            if (originalMessage.Author.Id == _client.CurrentUser.Id)
            {
                await originalMessage.RemoveAllReactionsAsync();

                _embedBuilder
                    .WithFooter(footer => footer.Text = "Thank you for balancing the teams! Good luck – and have fun! :) \n\n")
                    .WithTitle("TEAM SKILL BALANCE")
                    .WithDescription(GetMessage());
                //.WithTimestamp();

                await originalMessage.ModifyAsync(message =>
                {
                    message.Embed = _embedBuilder.Build();
                });
            }
        }

        private static string GetMessage()
        {
            string message = "";

            double skillWest = PlayerList.GetSideTotalSkill(Side.WEST);
            double skillEast = PlayerList.GetSideTotalSkill(Side.EAST);

            int playersCountWest = PlayerList.GetPlayerCountOnSide(Side.WEST);
            int playersCountEast = PlayerList.GetPlayerCountOnSide(Side.EAST);

            double percentageLowerLimit = 0.85;
            double percentageUpperLimit = 1.20;

            if (Match.GetMap() == "None")
            {
                message = "\n**A new match is currently starting!**";
            }
            else
            {

                // Saved for later possible use
                /*
                message =
                "\n \nBLUFOR total skill: **"
                + skillWest
                + "**\nOPFOR total skill:  **"
                + skillEast
                + "** \n \nYou should join: ";
                */

                message += "\n\nTo ensure fair and fun gaming experience for \neveryone, **please join the following side**: \n\n-------------\n";

                if (skillWest == skillEast)
                {
                    message += PlayerList.GetSideEmote(Side.WEST) + " **OR** " + PlayerList.GetSideEmote(Side.EAST) + " **- choose freely! :)**";
                }
                else
                {
                    message += PlayerList.GetSideEmote(PlayerList.GetSideWherePlayerShouldJoin()) + " **" + PlayerList.GetSideWherePlayersShouldJoinString() + "**";
                }

                message += "\n-------------\n";

                // message += "Total skill (BLUFOR): **" + skillWest + "** \nTotal skill (OPFOR):  **" + skillEast + "**";

                // message += "\n-------------\n\n";

                // If player count is low,
                // warn players about possibly quickly changing side value
                if ((playersCountWest + playersCountEast) < 8)
                {
                    message += "** -- NOTE -- ** \nBecause of the current player count, the \nteam that you should join might change quickly.";
                }
                else
                {
                    // If skill difference between teams is within given threshold,
                    // warn players about possibly quickly changing side value
                    if (
                        ((skillWest * percentageLowerLimit) > skillEast) ||
                        ((skillWest * percentageUpperLimit) < skillEast) ||
                        ((skillEast * percentageLowerLimit) > skillWest) ||
                        ((skillEast * percentageUpperLimit) < skillWest))
                    {
                        // message += "** -- NOTE -- ** \nThe skill difference between teams is very small at this moment.\nBecause of that, the team that you should join might change quickly.";
                    }
                }

                long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                string relativeTime = $"<t:{currentTime}:R>";

                message +=
                    "\n\nPlayers ingame: "
                    + "**"
                    + (PlayerList.GetPlayerCountOnSide(Side.WEST) + PlayerList.GetPlayerCountOnSide(Side.EAST))
                    + "**";

                string currentMap = Match.GetMap();

                if (currentMap == "Chernarus")
                {
                    message += "\n\nCurrent map: "
                    + "**"
                    + currentMap + " :park:"
                    + "**";
                }
                else if (currentMap == "Takistan")
                {
                    message += "\n\nCurrent map: "
                   + "**"
                   + currentMap + " :desert:"
                   + "**";
                }
                message += "\n\n"
                    + "This status was updated: " + relativeTime
                    + "\n";
            }

            return message;
        }
    }
}