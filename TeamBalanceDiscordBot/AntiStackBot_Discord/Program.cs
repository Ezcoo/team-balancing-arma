using AntiStackBot_Discord;
using Discord;
using Discord.WebSocket;
using System.Threading;
using System.Threading.Tasks;

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
            var token = "ADD_YOUR_TOKEN_HERE";

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            // _client.MessageUpdated += MessageUpdated;

            Log.Write("Initializing the bot...", LogLevel.INFO);

            _client.Ready += () =>
            {
                Log.Write("Bot is connected!", LogLevel.IMPORTANT);
                return Task.CompletedTask;
            };

            Log.Write("INIT DONE", LogLevel.INFO);

            _client.Connected += MessageCreate;

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        private static async Task MessageCreate()
        {
            Thread.Sleep(5100);
            
            // Add your channel here
            IMessageChannel channel = _client.GetChannel(0000000000000) as IMessageChannel;
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
            Thread.Sleep(5100);

            if (originalMessage.Author.Id == _client.CurrentUser.Id)
            {
                await originalMessage.RemoveAllReactionsAsync();

                _embedBuilder
                    .WithFooter(footer => footer.Text = "Thank you for balancing the teams! Good luck – and have fun! :) \n\nThis status was updated: ")
                    .WithTitle("TEAM SKILL BALANCE")
                    .WithDescription(GetMessage())
                    .WithCurrentTimestamp();

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
                message = "\n**Match is currently restarting!**";
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

                message += "\n-------------\n\n"; 
                
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
                        message += "** -- NOTE -- ** \nThe skill difference between teams is very small at this moment.\nBecause of that, the team that you should join might change quickly.";
                    }
                }

                message += 
                    "\n\nPlayers ingame: "
                    + "**"
                    + (PlayerList.GetPlayerCountOnSide(Side.WEST) + PlayerList.GetPlayerCountOnSide(Side.EAST))
                    + "**"
                    + "\n\nCurrent map: "
                    + "**"
                    + Match.GetMap()
                    + "**"
                    + "\n";
            }

            return message;
        }
    }
}