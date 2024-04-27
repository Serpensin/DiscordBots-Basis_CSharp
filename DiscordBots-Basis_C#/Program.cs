using Microsoft.Extensions.Logging;

namespace C_
{
    public class Program
    {
        private static AClient client = new AClient();
        private static async Task Main()
        {
            EnvironmentVariables envVars = new EnvironmentVariables();
            envVars.Load();

            // Create Folders
            Directory.CreateDirectory(EnvironmentVariables.LogFolder);
            Directory.CreateDirectory(EnvironmentVariables.BufferFolder);

            LogManager.Initialize(EnvironmentVariables.LogFolder, EnvironmentVariables.BotName);
            ILogger SentryLogger = LogManager.GetLogger("Sentry");
            ILogger ManLogger = LogManager.GetLogger("Program");
            ManLogger.LogInformation("Engine powering up...");

            if (!string.IsNullOrEmpty(EnvironmentVariables.SentryDSN))
            {
                SentrySdk.Init(o =>
                {
                    o.Dsn = EnvironmentVariables.SentryDSN;
                    o.AutoSessionTracking = true;
                    o.IsGlobalModeEnabled = true;
                    o.EnableTracing = true;
                    o.TracesSampleRate = 1.0;
                    o.Environment = "Development";
                    o.Release = EnvironmentVariables.BotVersion;
                    o.Debug = EnvironmentVariables.LoggingLevel == "Debug";
                    o.StackTraceMode = StackTraceMode.Enhanced;
                });
            }

            Console.CancelKeyPress += new ConsoleCancelEventHandler(shutdownHandler);
            await client.ConnectAsync();
            await Task.Delay(-1);
        }
        private static void shutdownHandler(object sender, ConsoleCancelEventArgs args)
        {
            args.Cancel = true;
            client.ShutdownAsync(null);
        }
    }
}
