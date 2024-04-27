﻿namespace C_
{
    public class EnvironmentVariables
    {
        public static string BotToken { get; private set; }
        public static ulong OwnerID { get; private set; }
        public static string ?SentryDSN { get; private set; }
        public static string BotName { get; private set; }
        public static string AppFolderName { get; private set; }
        public static string LogFolder { get; private set; }
        public static string BufferFolder { get; private set; }
        public static string ActivityFile { get; private set; }
        public static string BotVersion { get; private set; }
        public static string LoggingLevel { get; private set; }

        public void Load()
        {
            DotNetEnv.Env.Load();

            BotToken = Environment.GetEnvironmentVariable("TOKEN");
            OwnerID = ulong.Parse(Environment.GetEnvironmentVariable("OWNER_ID"));
            SentryDSN = Environment.GetEnvironmentVariable("SENTRY_DSN");
            LoggingLevel = Environment.GetEnvironmentVariable("LOGGING_LEVEL");
            BotName = "BotName";
            AppFolderName = "AppFolderName";
            LogFolder = Path.Combine(AppFolderName, "Logs");
            BufferFolder = Path.Combine(AppFolderName, "Buffer");
            ActivityFile = Path.Combine(AppFolderName, "activity.json");
            BotVersion = "1.0.0";
        }
    }
}
