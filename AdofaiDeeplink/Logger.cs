using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using System.Threading;

namespace AdofaiDeeplink
{
    public static class Logger
    {
        private static bool hasWrittenEntry;

        public static readonly string LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pro203S");

        public static void Info(string message) => Log("INFO", message);
        public static void Warn(string message) => Log("WARN", message);
        public static void Error(string message, Exception exception = null) =>
            Log("ERROR", exception == null ? message : message + Environment.NewLine + exception);

        private static void Log(string level, string message)
        {
            try
            {
                WriteEntry(LogDirectory, level, message);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException)
            {
                // A logging failure must not interrupt installation or opening a level.
                Debug.WriteLine("Unable to write AdofaiDeeplink log: " + ex);
            }
        }

        internal static void WriteEntry(string directory, string level, string message)
        {
            // Serialize rotation and append across threads and processes in this session.
            using (var mutex = new Mutex(false, @"Local\Pro203S.AdofaiDeeplink.Logger"))
            {
                try
                {
                    mutex.WaitOne();
                }
                catch (AbandonedMutexException)
                {
                    // The previous process exited; this process now owns the mutex.
                }

                try
                {
                    DateTime now = DateTime.Now;
                    Directory.CreateDirectory(directory);
                    string latest = Path.Combine(directory, "AdofaiDeeplink-latest.log");
                    string previous = Path.Combine(directory, "AdofaiDeeplink-latest-previous.log");

                    if (File.Exists(latest) && File.GetLastWriteTime(latest).Date != now.Date)
                    {
                        if (File.Exists(previous))
                            File.Replace(latest, previous, null);
                        else
                            File.Move(latest, previous);
                    }

                    string entry = "[" + now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)
                        + "] [" + level + "] " + message + Environment.NewLine;
                    if (!hasWrittenEntry)
                        entry = Environment.NewLine + entry;

                    File.AppendAllText(latest, entry, new UTF8Encoding(false));
                    hasWrittenEntry = true;
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
        }
    }
}
