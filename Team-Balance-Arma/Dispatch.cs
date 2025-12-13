using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace TeamBalanceArma
{
    public class Dispatch
    {
        // Necessary stuff for Arma 2 to work
        [A2WASPDatabase.DllExport("_RVExtension@12", CallingConvention = CallingConvention.StdCall)]

        // ****************************************************************************
        // This is the entry point of the DLL from Arma 2, some necessary code included
        // ****************************************************************************

        // Note that the whole application works asynchronously since calling any DLL from Arma is a blocking operation!
        // Combined with high/extreme performance needs of the game and relatively slow database operations (by nature) we need to go async
        public static void RvExtension(StringBuilder output, int outputSize, [MarshalAs(UnmanagedType.LPStr)] string args)
        {
            // We need to wrap everything in a try-catch block because not using it will crash the whole server in case of a bug
            // There are nested try-catch blocks for more accurate error messages (could also throw them but this way we probably get more info about the bug)
            try
            {
                Log.Write("Database extension was called with arguments (starting from status code): " + args, LogLevel.VERBOSE);

                // We use numeral procedure codes since it's a lot easier to handle (just numbers in input then) and we don't need that many different codes
                List<long> arguments = SplitInput(args);

                long procedureCode = arguments[0];
                
                // This way (combined with not too many procedure codes) handling the code is a lot easier since the whole input is formatted as string containing numbers in array
                switch (procedureCode)
                {
                    case 101:
                        output.Append(PlayerSkill.RetrievePlayer(arguments));
                        break;
                    case 202:
                        output.Append(PlayerSkill.StorePlayer(arguments));
                        break;
                    case 303:
                        output.Append(Players.ListPlayersOnServer(arguments));
                        break;
                    case 404:
                        output.Append(Players.StoreSideOfPlayer(arguments));
                        break;
                    case 505:
                        output.Append(PlayerSkill.TryRetrieve(arguments));
                        break;
                    case 606:
                        output.Append(PlayerSkill.GetSideTotalSkill(arguments));
                        break;
                    case 707:
                        output.Append(PlayerSkill.TryRetrieveSideTotalSkill(arguments));
                        break;
                    case 808:
                        Players.FlushSideInfoOfAllPlayers();
                        output.Append("[1]");
                        break;
                    case 909:
                    {
                        long mapId = arguments[1];
                        output.Append(Match.SaveMap(Match.ConvertLongToMap(mapId)));
                        break;
                    }
                }
            }
            catch (Exception error)
            {
                Log.Write("CRITICAL ERROR! " + error.ToString(), LogLevel.CRITICAL);
                output.Append("[-111,1,1]");
            };
        }

        public static List<long> SplitInput(string input)
        {
            string[] delimiter = { "," };
            int count = input.Length;
            string[] argumentsStringArray = input.Split(delimiter, count, StringSplitOptions.RemoveEmptyEntries);
            List<long> arguments = new List<long>();

            try
            {
                arguments.AddRange(Array.ConvertAll(argumentsStringArray, long.Parse));
            }
            catch (Exception error)
            {
                Log.Write("ERROR! Arguments: " + argumentsStringArray + ", error message: " + error.ToString(), LogLevel.ERROR);
            }

            Log.Write("" + argumentsStringArray, LogLevel.VERBOSE);

            return arguments;
        }
    }
}
    
