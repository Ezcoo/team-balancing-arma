using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace TeamBalanceArma
{
    public static class Log
    {
        public static async void Write(string message,
            LogLevel logLevel,
            // Ugly fix, see FileManager.cs beginning for more info why this is still being used
            bool callingFromCircularDependency = false,
            bool omitConsoleMessage = false,
            [CallerFilePath] string file = "",
            [CallerMemberName] string member = "",
            [CallerLineNumber] int line = 0)
        {
            await WriteAsync(message, logLevel, callingFromCircularDependency, omitConsoleMessage, file, member, line);
        }

        public static Task WriteAsync(string message,
            LogLevel logLevel,
            bool callingFromCircularDependency = false,
            bool omitConsoleMessage = false,
            [CallerFilePath] string file = "",
            [CallerMemberName] string member = "",
            [CallerLineNumber] int line = 0)
        {
            CultureInfo culture = CultureInfo.InvariantCulture;

            string date = DateTime.Now.Date.ToString("dd.MM.yyyy", culture);
            string time = DateTime.Now.ToString("hh:mm:ss.fff", culture);

            NormalizeOutput(logLevel);

            string logMessageToFile = date + " " + time + " - [" + logLevel + "]: " + Path.GetFileName(file) + ": " + member + "()" + ", line " + line + ": " + message;

            if (logLevel <= LogLevel.DEBUG)
            {
                // Ugly fix to avoid circular dependency... See the beginning of FileManager.cs for more info
                if (callingFromCircularDependency)
                {
                    WriteToFileSelfContained(logLevel, logMessageToFile);
                }
                else
                {
                    WriteToFile(logLevel, logMessageToFile);
                }
            }

            // We don't need this for now since DLLs generally can't print console messages
        
            /*
            string colorCodeStart = GetColorCode(logLevel);
            string colorCodeEnd = "\u001b[0m";

            string logMessage = colorCodeStart + date + " " + time + " - [LOG] [" + logLevel + "]: " + emptySpace + " " + Path.GetFileName(file) + ": " + member + "()" + ", line " + line + ": " + message + colorCodeEnd;

                
            if (logLevel <= LogLevel.DEBUG && !omitConsoleMessage)
            {
                Console.WriteLine(logMessage);
            }
            */

            return Task.CompletedTask;
        }

        private static string NormalizeOutput(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case (LogLevel.CRITICAL): return "--";
                case (LogLevel.IMPORTANT): return "-";
                case (LogLevel.WARNING): return "---";
                case (LogLevel.ERROR): return "-----";
                case (LogLevel.TRIVIAL): return "---";
                case (LogLevel.INFO): return "------";
                case (LogLevel.DEBUG): return "-----";
                case (LogLevel.VERBOSE): return "---";
                default: return "";
            }

        }

        private static void WriteToFile(LogLevel logLevel, string logMessage)
        {
            string fileToWriteToString = logLevel.ToString();
            string fileExtension = ".txt";

            // Log.WriteInternal("File contents: " + logMessage, LogLevel.VERBOSE);
            FileManager.CheckIfDirectoryAndFileExistAndCreateThemIfNecessaryAndAppend(GlobalVariables.LogsFolder, fileToWriteToString, fileExtension, logMessage);

            // Write also to general log file because DLLs can't have console as general output
            string fileToWriteToStringAll = "ALL";

            FileManager.CheckIfDirectoryAndFileExistAndCreateThemIfNecessaryAndAppend(GlobalVariables.LogsFolder, fileToWriteToStringAll, fileExtension, logMessage);

        }

        private static void WriteToFileSelfContained(LogLevel logLevel, string logMessage)
        {
            // Log.WriteInternal("Checking if directory " + GlobalVariables.logsDirectory + logLevel + " exists.", LogLevel.VERBOSE);
            CheckIfDirectoryAndFileExistAndCreateThemIfNecessaryAndAppend(GlobalVariables.LogsFolder, logLevel, logMessage);
        }

        private static void CheckIfDirectoryExistsAndCreateIfNecessary(string path, string file)
        {
            if (!Directory.Exists(path))
            {
                // Log.WriteInternal("Directory " + path + " doesn't exist, creating it.", LogLevel.ERROR);
                Directory.CreateDirectory(path);
                // Log.WriteInternal("Directory " + path + " was created.", LogLevel.VERBOSE);
            }
            else
            {
                // Log.WriteInternal("Directory " + path + " exists already.", LogLevel.VERBOSE);
            }

            // CheckIfFileExistsAndCreateItIfNecessary(path, file);

        }

        private static void CheckIfDirectoryAndFileExistAndCreateThemIfNecessaryAndAppend(string directory, LogLevel logLevel, string content)
        {
            string fileToWriteToString = logLevel.ToString();

            // Log.WriteInternal("Checking if directory " + GlobalVariables.logsDirectory + fileToWriteToString + " exists.", LogLevel.VERBOSE);

            CheckIfDirectoryExistsAndCreateIfNecessary(directory, fileToWriteToString);

            string fileExtension = ".txt";
            string pathToFile = directory + fileToWriteToString + fileExtension;

            // Log.WriteInternal("Writing to file: " + pathToFile, LogLevel.VERBOSE);
            // Log.WriteInternal("The contents of: " + pathToFile + ": \n" + content, LogLevel.VERBOSE);

            string contentWithNewLine = Environment.NewLine + content;

            File.AppendAllText(pathToFile, contentWithNewLine);

            // Log.WriteInternal("Done writing to file: " + pathToFile, LogLevel.VERBOSE);
        }

    }
}