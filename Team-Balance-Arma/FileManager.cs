using System.IO;
using System.Text;

namespace TeamBalanceArma
{
    static class FileManager
    {
        // (Very) ugly fix to prevent log writing from entering endless loop since the logging uses it's own methods to write to log - a proper solution would be to use external dependency for writing logs
        // Consider the included logging system as showcase (but not using it in itself, it's horrible practice!)
        // But hey, it works in this use case! ;)
        private static readonly bool CallingLogFromCircularDependency = true;

        public static void CheckIfDirectoryAndFileExistAndCreateThemIfNecessary(string path, string file, string fileExtension = "")
        {
            if (!Directory.Exists(path))
            {
                Log.Write("Directory " + path + " doesn't exist, creating it.", LogLevel.ERROR, CallingLogFromCircularDependency);
                Directory.CreateDirectory(path);
                Log.Write("Directory " + path + " was created.", LogLevel.INFO, CallingLogFromCircularDependency);
            }
            else
            {
                Log.Write("Directory " + path + " exists already.", LogLevel.VERBOSE, CallingLogFromCircularDependency);
            }

            CheckIfFileExistsAndCreateItIfNecessary(path, file, fileExtension);

        }

        public static void CheckIfFileExistsAndCreateItIfNecessary(string path, string file, string fileExtension = "")
        {
            string wholePath = path + file;

            if (!File.Exists(wholePath + fileExtension))
            {
                Log.Write("File doesn't exist: " + wholePath + ".", LogLevel.ERROR, CallingLogFromCircularDependency);
                CreateNewFile(wholePath, false, fileExtension);
                Log.Write("Done creating dummy file " + wholePath + ".", LogLevel.INFO, CallingLogFromCircularDependency);
            }
            else
            {
                Log.Write(wholePath + " exists already. Maybe it should be rewritten anyway?", LogLevel.VERBOSE, CallingLogFromCircularDependency);
            }
        }

        public static void CreateNewFile(string fileAndPath, bool fileExtensionIncluded = true, string fileExtension = "")
        {
            Log.Write("Creating new dummy file: " + fileAndPath + ".", LogLevel.INFO, CallingLogFromCircularDependency);

            if (fileExtensionIncluded)
            {
                using (FileStream fs = File.Create(fileAndPath))
                {
                    byte[] info = new UTF8Encoding(true).GetBytes("0");
                    fs.Write(info, 0, info.Length);
                    fs.Close();
                }
            }
            else
            {
                using (FileStream fs = File.Create(fileAndPath + fileExtension))
                {
                    byte[] info = new UTF8Encoding(true).GetBytes("0");
                    fs.Write(info, 0, info.Length);
                    fs.Close();
                }
            }
            Log.Write("Done creating new dummy file: " + fileAndPath + ".", LogLevel.INFO, CallingLogFromCircularDependency);
        }

        public static void CheckIfDirectoryAndFileExistAndCreateThemIfNecessaryAndWrite(string directory, string file, string fileExtension, string content)
        {
            CheckIfDirectoryAndFileExistAndCreateThemIfNecessary(directory, file);
            string pathToFile = directory + file;
            Log.Write("Writing to file: " + pathToFile, LogLevel.VERBOSE, CallingLogFromCircularDependency);
            Log.Write("The contents of: " + pathToFile + ": \n" + content, LogLevel.VERBOSE, CallingLogFromCircularDependency);

            FileStream fs = new FileStream(pathToFile + fileExtension, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            StreamWriter sw = new StreamWriter(fs, Encoding.UTF8);
            sw.BaseStream.Seek(0, SeekOrigin.End);
            sw.WriteLine(content);
            sw.Close();

            Log.Write("Done writing to file: " + pathToFile, LogLevel.VERBOSE, CallingLogFromCircularDependency);
        }

        public static void CheckIfDirectoryAndFileExistAndCreateThemIfNecessaryAndAppend(string directory, string file, string fileExtension, string content)
        {
            CheckIfDirectoryAndFileExistAndCreateThemIfNecessary(directory, file, fileExtension);
            string pathToFile = directory + file;
            Log.Write("Writing to file: " + pathToFile, LogLevel.VERBOSE, CallingLogFromCircularDependency);
            Log.Write("The contents of: " + pathToFile + ": \n" + content, LogLevel.VERBOSE, CallingLogFromCircularDependency);

            FileStream fs = new FileStream(pathToFile + fileExtension, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            StreamWriter sw = new StreamWriter(fs, Encoding.UTF8);
            sw.BaseStream.Seek(0, SeekOrigin.End);
            sw.WriteLine(content);
            sw.Close();

            Log.Write("Done appending to file: " + pathToFile, LogLevel.VERBOSE, CallingLogFromCircularDependency);
        }
    }
}
