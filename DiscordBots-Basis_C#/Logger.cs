using Discord;
using Microsoft.Extensions.Logging;

namespace Basis
{
    public static class DiscordExtensions
    {
        public static LogLevel ToLogLevel(this LogSeverity severity)
        {
            switch (severity)
            {
                case LogSeverity.Critical:
                    return LogLevel.Critical;
                case LogSeverity.Error:
                    return LogLevel.Error;
                case LogSeverity.Warning:
                    return LogLevel.Warning;
                case LogSeverity.Info:
                    return LogLevel.Information;
                case LogSeverity.Verbose:
                case LogSeverity.Debug:
                    return LogLevel.Debug;
                default:
                    return LogLevel.None;
            }
        }
    }

    public static class LogManager
    {
        private static ILoggerFactory _loggerFactory;
        private static LogLevel ParseLogLevel(string levelString, LogLevel fallback)
        {
            if (Enum.TryParse<LogLevel>(levelString, true, out LogLevel level))
            {
                return level;
            }
            else
            {
                Console.WriteLine($"Invalid logging level '{levelString}'. Falling back to '{fallback}'.");
                return fallback;
            }
        }

        public static void Initialize(string logFolder, string loggerName)
        {
            string loggingLevelString = EnvironmentVariables.LoggingLevel;
            LogLevel minLogLevel = ParseLogLevel(loggingLevelString, LogLevel.Warning);

            ILoggerProvider loggerProvider = new TimedRotatingFileLoggerProvider(logFolder, loggerName, minLogLevel);
            _loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddProvider(loggerProvider).SetMinimumLevel(minLogLevel);
            });
        }

        public static ILogger GetLogger(string categoryName)
        {
            return _loggerFactory.CreateLogger(categoryName);
        }
    }

    public class TimedRotatingFileLoggerProvider : ILoggerProvider
    {
        private readonly string _logFolder;
        private readonly string _loggerName;
        private readonly LogLevel _minLogLevel;

        public TimedRotatingFileLoggerProvider(string logFolder, string loggerName, LogLevel minLogLevel)
        {
            _logFolder = logFolder;
            _loggerName = loggerName;
            _minLogLevel = minLogLevel;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new TimedRotatingFileLogger(_logFolder, _loggerName, categoryName, _minLogLevel);
        }

        public void Dispose()
        {
        }
    }

    public class TimedRotatingFileLogger : ILogger
    {
        private readonly string _logFolder;
        private readonly string _loggerName;
        private readonly string _categoryName;
        private readonly LogLevel _minLogLevel;
        private DateTime _lastRotationDate;
        private static readonly Mutex mutex = new Mutex();

        public TimedRotatingFileLogger(string logFolder, string loggerName, string categoryName, LogLevel minLogLevel)
        {
            _logFolder = logFolder;
            _loggerName = loggerName;
            _categoryName = categoryName;
            _minLogLevel = minLogLevel;
            _lastRotationDate = DateTime.Today;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= _minLogLevel;
        }

        public async void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            string logLevelString = logLevel.ToString().ToUpperInvariant();
            string paddedLogLevel = logLevelString.PadRight(11);

            string logMessage = "N/A";
            if (state.ToString() == "[null]" || exception is not null)
            {
                logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{paddedLogLevel}] {_categoryName}: {exception.Message}";
            }
            else
            {
                logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{paddedLogLevel}] {_categoryName}: {state.ToString()}";
            }

            string logFilePath = Path.Combine(_logFolder, $"{_loggerName}.log");

            FileInfo fileInfo = new FileInfo(logFilePath);
            if (fileInfo.Exists && fileInfo.CreationTime.Date != DateTime.Today)
            {
                string currentTime = fileInfo.CreationTime.ToString("yyyy-MM-dd");
                string currentLogFilePath = $"{logFilePath}.{currentTime}";

                try
                {
                    File.Move(logFilePath, currentLogFilePath);
                }
                catch (Exception ex)
                {
                    SentrySdk.CaptureException(ex);
                }
                finally
                {
                    File.Create(logFilePath).Close();
                    File.SetCreationTime(logFilePath, DateTime.Now);
                }

                _lastRotationDate = DateTime.Today;

                foreach (var file in new DirectoryInfo(_logFolder).GetFiles())
                {
                    if (file.CreationTime < DateTime.Now.AddDays(-28))
                    {
                        file.Delete();
                    }
                }
            }
            try
            {
                mutex.WaitOne();
                await Console.Out.WriteLineAsync(logMessage);
                using (StreamWriter writer = File.AppendText(logFilePath))
                {
                    await writer.WriteLineAsync(logMessage);
                }
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }
    }
}