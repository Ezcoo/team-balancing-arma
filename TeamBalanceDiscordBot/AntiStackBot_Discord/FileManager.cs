using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace A2WASPDiscordBot_Windows_App
{
    static class FileManager
    {
        private static readonly bool callingLogFromCircularDependency = true;

        public static void CheckIfDirectoryAndFileExistAndCreateThemIfNecessary(string path, string file, string fileExtension = "")
        {
            if (!Directory.Exists(path))
            {
                Log.Write("Directory " + path + " doesn't exist, creating it.", LogLevel.ERROR, callingLogFromCircularDependency);
                Directory.CreateDirectory(path);
                Log.Write("Directory " + path + " was created.", LogLevel.INFO, callingLogFromCircularDependency);
            }
            else
            {
                Log.Write("Directory " + path + " exists already.", LogLevel.VERBOSE, callingLogFromCircularDependency);
            }

            CheckIfFileExistsAndCreateItIfNecessary(path, file, fileExtension);

        }

        public static void CheckIfFileExistsAndCreateItIfNecessary(string _path, string _file, string fileExtension = "")
        {
            string _wholePath = _path + _file;

            if (!File.Exists(_wholePath + fileExtension))
            {
                Log.Write("File doesn't exist: " + _wholePath + ".", LogLevel.ERROR, callingLogFromCircularDependency);
                CreateNewFile(_wholePath, false, fileExtension);
                Log.Write("Done creating dummy file " + _wholePath + ".", LogLevel.INFO, callingLogFromCircularDependency);
            }
            else
            {
                Log.Write(_wholePath + " exists already. Maybe it should be rewritten anyway?", LogLevel.VERBOSE, callingLogFromCircularDependency);
            }
        }

        public static void CreateNewFile(string _fileAndPath, bool fileExtensionIncluded = true, string fileExtension = "")
        {
            Log.Write("Creating new dummy file: " + _fileAndPath + ".", LogLevel.INFO, callingLogFromCircularDependency);

            if (fileExtensionIncluded)
            {
                using (FileStream fs = File.Create(_fileAndPath))
                {
                    byte[] info = new UTF8Encoding(true).GetBytes("0");
                    fs.Write(info, 0, info.Length);
                    fs.Close();
                }
            }
            else
            {
                using (FileStream fs = File.Create(_fileAndPath + fileExtension))
                {
                    byte[] info = new UTF8Encoding(true).GetBytes("0");
                    fs.Write(info, 0, info.Length);
                    fs.Close();
                }
            }
            Log.Write("Done creating new dummy file: " + _fileAndPath + ".", LogLevel.INFO, callingLogFromCircularDependency);
        }

        public static void CheckIfDirectoryAndFileExistAndCreateThemIfNecessaryAndWrite(string directory, string file, string fileExtension, string content)
        {
            CheckIfDirectoryAndFileExistAndCreateThemIfNecessary(directory, file);
            string pathToFile = directory + file;
            Log.Write("Writing to file: " + pathToFile, LogLevel.VERBOSE, callingLogFromCircularDependency);
            Log.Write("The contents of: " + pathToFile + ": \n" + content, LogLevel.VERBOSE, callingLogFromCircularDependency);
            File.WriteAllText(pathToFile + fileExtension, content);
            Log.Write("Done writing to file: " + pathToFile, LogLevel.VERBOSE, callingLogFromCircularDependency);
        }

        public async static void CheckIfDirectoryAndFileExistAndCreateThemIfNecessaryAndAppend(string directory, string file, string fileExtension, string content)
        {
            CheckIfDirectoryAndFileExistAndCreateThemIfNecessary(directory, file, fileExtension);
            string pathToFile = directory + file;
            Log.Write("Writing to file: " + pathToFile, LogLevel.VERBOSE, callingLogFromCircularDependency);
            Log.Write("The contents of: " + pathToFile + ": \n" + content, LogLevel.VERBOSE, callingLogFromCircularDependency);

            if (File.Exists(pathToFile))
            {
                using (StreamWriter DestinationWriter = File.CreateText(pathToFile))
                {
                    await AppendText(pathToFile + fileExtension, content);
                }

                Log.Write("Done appending to file: " + pathToFile, LogLevel.VERBOSE, callingLogFromCircularDependency);
            }
        }

        private static Task AppendText(string _pathToFileAndFileExtension, string _content)
        {
            File.AppendAllText(_pathToFileAndFileExtension, _content + Environment.NewLine);
            return Task.CompletedTask;
        }

    }
}