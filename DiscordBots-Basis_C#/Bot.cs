using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.IO.Compression;
using System.Net;

namespace Basis
{
    public class AClient : DiscordShardedClient
    {
        private bool isInitialized = false;
        private readonly DiscordSocketClient _client;
        private readonly ILogger Logger = LogManager.GetLogger("Discord");
        private readonly ILogger ManLogger = LogManager.GetLogger("Program");
        private readonly ActivityValidator validator = new ActivityValidator(EnvironmentVariables.ActivityFile);
        public AClient()
        {
            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
                AlwaysDownloadUsers = true,
                LogLevel = GetLogLevel(),
                MessageCacheSize = 1000,
                GatewayIntents = GatewayIntents.MessageContent |
                                 GatewayIntents.DirectMessages |
                                 GatewayIntents.Guilds |
                                 GatewayIntents.GuildMessages |
                                 GatewayIntents.GuildMembers,
                ConnectionTimeout = 30000,
                TotalShards = 0,
                UseSystemClock = true
            });
            _client.Log += LogAsync;

            _client.SetStatusAsync(UserStatus.Invisible);
            _client.GuildAvailable += OnGuildJoin;
            _client.MessageReceived += OnMessage;
            _client.JoinedGuild += OnGuildJoin;
            _client.LeftGuild += OnGuildRemove;
            _client.Ready += OnReady;
            _client.UserJoined += OnUserJoin;
            _client.UserLeft += OnUserLeave;

        }

        public async Task ConnectAsync()
        {
            try
            {
                await _client.LoginAsync(TokenType.Bot, EnvironmentVariables.BotToken);
                await _client.StartAsync();
                //await Task.Delay(-1);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error occurred while connecting the bot.");
            }
        }
        public async Task ShutdownAsync(SocketMessage message)
        {
            string _message = "Engine powering down...";
            ManLogger.LogInformation(_message);
            if (message != null)
            {
                await message.Channel.SendMessageAsync(_message);
            }
            else
            {
            }

            await _client.SetStatusAsync(UserStatus.Invisible);
            _client.PurgeChannelCache();
            _client.PurgeDMChannelCache();
            _client.PurgeUserCache();

            _client.Log -= LogAsync;

            await _client.StopAsync();
            Environment.Exit(0);
        }


        private async Task SetActivity()
        {
            validator.ValidateAndFixJson();
            string json = File.ReadAllText(EnvironmentVariables.ActivityFile);
            dynamic data = JsonConvert.DeserializeObject(json);
            string activityType = data.activity_type;
            string activityTitle = data.activity_title;
            string activityUrl = data.activity_url;

            switch (activityType)
            {
                case "Playing":
                    await _client.SetActivityAsync(new Game(activityTitle));
                    break;
                case "Streaming":
                    await _client.SetActivityAsync(new StreamingGame(activityTitle, activityUrl));
                    break;
                case "Listening":
                    Game listeningActivity = new(
                        activityTitle,
                        ActivityType.Listening
                    );
                    await _client.SetActivityAsync(listeningActivity);
                    break;
                case "Watching":
                    Game watchingActivity = new(
                        activityTitle,
                        ActivityType.Watching
                    );
                    await _client.SetActivityAsync(watchingActivity);
                    break;
                case "Competing":
                    Game competingActivity = new(
                        activityTitle,
                        ActivityType.Competing
                    );
                    await _client.SetActivityAsync(competingActivity);
                    break;
                default:
                    await _client.SetActivityAsync(null);
                    break;
            }
        }
        private async Task SetStatus()
        {
            validator.ValidateAndFixJson();
            string json = File.ReadAllText(EnvironmentVariables.ActivityFile);
            dynamic data = JsonConvert.DeserializeObject(json);
            string status = data.status;

            switch (status)
            {
                case "online":
                    await _client.SetStatusAsync(UserStatus.Online);
                    break;
                case "idle":
                    await _client.SetStatusAsync(UserStatus.Idle);
                    break;
                case "dnd":
                    await _client.SetStatusAsync(UserStatus.DoNotDisturb);
                    break;
                case "invisible":
                    await _client.SetStatusAsync(UserStatus.Invisible);
                    break;
                default:
                    await _client.SetStatusAsync(UserStatus.Online);
                    break;
            }
        }
        private Task SaveActivity(string activityType, string activityTitle, string activityUrl)
        {
            string json = File.ReadAllText(EnvironmentVariables.ActivityFile);
            dynamic data = JsonConvert.DeserializeObject(json);
            if (string.IsNullOrEmpty(activityType))
            {
                data.activity_type = "";
                data.activity_title = "";
                data.activity_url = "";
            }
            else
            {
                data.activity_type = char.ToUpper(activityType[0]) + activityType.Substring(1);
                data.activity_title = activityTitle;
                data.activity_url = activityUrl;
            }

            string newJson = JsonConvert.SerializeObject(data, Formatting.Indented);
            if (isActivityUnchanged(newJson))
            {
                throw new Exception("Activity is unchanged.");
            }
            File.WriteAllText(EnvironmentVariables.ActivityFile, newJson);
            return Task.CompletedTask;
        }
        private Task SaveStatus(string status)
        {
            string json = File.ReadAllText(EnvironmentVariables.ActivityFile);
            dynamic data = JsonConvert.DeserializeObject(json);
            data.status = status;
            string newJson = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(EnvironmentVariables.ActivityFile, newJson);
            return Task.CompletedTask;
        }
        private bool isActivityUnchanged(string newJson)
        {
            string jsonOld = File.ReadAllText(EnvironmentVariables.ActivityFile);
            dynamic dataOld = JsonConvert.DeserializeObject(jsonOld);
            dynamic dataNew = JsonConvert.DeserializeObject(newJson);
            return dataOld.activity_type == dataNew.activity_type &&
                   dataOld.activity_title == dataNew.activity_title &&
                   dataOld.activity_url == dataNew.activity_url;
        }

        private async Task OwnerLogAsync(SocketMessage message, string[] args)
        {
            async Task _WrongSelectionAsync()
            {
                await message.Channel.SendMessageAsync("```" +
                                                    "log [current/folder/lines] (Replace lines with a positive number, if you only want lines.) - Get the log\n" +
                                                    "```");
            }

            if (!args.Any())
            {
                await _WrongSelectionAsync();
                return;
            }

            if (args[0] == "current")
            {
                string _currentLog = $"{Path.Combine(EnvironmentVariables.LogFolder, EnvironmentVariables.BotName)}.log";
                string _tempLog = $"{Path.Combine(EnvironmentVariables.BufferFolder, EnvironmentVariables.BotName)}.log";
                File.Copy(_currentLog, _tempLog, true);
                try
                {
                    await message.Channel.SendFileAsync(_tempLog);
                }
                catch (Discord.Net.HttpException ex)
                {
                    if (ex.HttpCode == HttpStatusCode.RequestEntityTooLarge)
                    {
                        ManLogger.LogDebug($"Log file too large. Sending as archive.");

                        string _zipfile = $"{Path.Combine(EnvironmentVariables.BufferFolder, EnvironmentVariables.BotName)}.zip";
                        using (var archive = ZipFile.Open(_zipfile, ZipArchiveMode.Create))
                        {
                            archive.CreateEntryFromFile(_tempLog, Path.GetFileName(_tempLog), compressionLevel: CompressionLevel.SmallestSize);
                        }
                        try
                        {
                            await message.Channel.SendFileAsync(_zipfile);
                        }
                        catch (Exception)
                        {
                            await message.Channel.SendMessageAsync("Log file too large to send.");
                            throw;
                        }
                        finally
                        {
                            if (File.Exists(_zipfile))
                            {
                                File.Delete(_zipfile);
                            }
                        }
                    }
                    else
                    {
                        ManLogger.LogError($"Error sending log file: {ex.Message}");
                        throw;
                    }
                }
                finally
                {
                    if (File.Exists(_tempLog))
                    {
                        File.Delete(_tempLog);
                    }
                }
            }
            else if (args[0] == "folder")
            {
                string _zipfile = $"{Path.Combine(EnvironmentVariables.BufferFolder, EnvironmentVariables.BotName)}.zip";
                string _templogfolder = Path.Combine(EnvironmentVariables.BufferFolder, "TEMP_LOG");
                Directory.CreateDirectory(_templogfolder);
                // Copy all log files to temp folder
                foreach (var file in new DirectoryInfo(EnvironmentVariables.LogFolder).GetFiles())
                {
                    file.CopyTo(Path.Combine(_templogfolder, file.Name), true);
                }
                using (var archive = ZipFile.Open(_zipfile, ZipArchiveMode.Create))
                {
                    foreach (var file in new DirectoryInfo(_templogfolder).GetFiles())
                    {
                        archive.CreateEntryFromFile(file.FullName, file.Name, compressionLevel: CompressionLevel.SmallestSize);
                    }
                }
                try
                {
                    await message.Channel.SendFileAsync(_zipfile);
                }
                catch (Discord.Net.HttpException ex)
                {
                    if (ex.HttpCode == HttpStatusCode.RequestEntityTooLarge)
                    {
                        await message.Channel.SendMessageAsync("The folder is too big to be sent directly.\nPlease get the current file or the last X lines.");
                    }
                    else
                    {
                        ManLogger.LogError($"Error sending log folder: {ex.Message}");
                        throw;
                    }
                }
                finally
                {
                    if (File.Exists(_zipfile))
                    {
                        File.Delete(_zipfile);
                    }
                    if (Directory.Exists(_templogfolder))
                    {
                        Directory.Delete(_templogfolder, true);
                    }
                }
            }
            else
            {
                int lines = 0;
                try
                {
                    if (int.Parse(args[0]) < 1)
                    {
                        await _WrongSelectionAsync();
                        return;
                    }
                    else
                    {
                        lines = int.Parse(args[0]);
                    }
                }
                catch (FormatException)
                {
                    await _WrongSelectionAsync();
                    return;
                }

                string _currentLog = $"{Path.Combine(EnvironmentVariables.LogFolder, EnvironmentVariables.BotName)}.log";
                string _tempLog = $"{Path.Combine(EnvironmentVariables.BufferFolder, EnvironmentVariables.BotName)}-{lines}_lines.log";

                using (StreamReader reader = new StreamReader(_currentLog))
                {
                    using (StreamWriter writer = new StreamWriter(_tempLog))
                    {
                        List<string> lastLines = new List<string>();

                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            lastLines.Add(line);
                            if (lastLines.Count > lines)
                            {
                                lastLines.RemoveAt(0);
                            }
                        }

                        foreach (string lastLine in lastLines)
                        {
                            writer.WriteLine(lastLine);
                        }
                    }
                }

                try
                {
                    await message.Channel.SendFileAsync(_tempLog, text: $"Last {lines} lines of the log file.");
                }
                catch (Discord.Net.HttpException ex)
                {
                    if (ex.HttpCode == HttpStatusCode.RequestEntityTooLarge)
                    {
                        ManLogger.LogDebug($"Log file too large. Sending as archive.");

                        string _zipfile = $"{Path.Combine(EnvironmentVariables.BufferFolder, EnvironmentVariables.BotName)}.zip";
                        using (var archive = ZipFile.Open(_zipfile, ZipArchiveMode.Create))
                        {
                            archive.CreateEntryFromFile(_tempLog, Path.GetFileName(_tempLog), compressionLevel: CompressionLevel.SmallestSize);
                        }
                        try
                        {
                            await message.Channel.SendFileAsync(_zipfile, text: $"Last {lines} lines of the log file.");
                        }
                        catch (Exception)
                        {
                            await message.Channel.SendMessageAsync("Log file too large to send.");
                            throw;
                        }
                        finally
                        {
                            if (File.Exists(_zipfile))
                            {
                                File.Delete(_zipfile);
                            }
                        }
                    }
                    else
                    {
                        ManLogger.LogError($"Error sending log file: {ex.Message}");
                        throw;
                    }
                }
                finally
                {
                    if (File.Exists(_tempLog))
                    {
                        File.Delete(_tempLog);
                    }
                }
            }
        }
        private async Task OwnerStatusAsync(SocketMessage message, string[] args)
        {
            async Task _WrongSelectionAsync()
            {
                await message.Channel.SendMessageAsync("```" +
                                                       "status [online/idle/dnd/invisible] - Set the status of the bot\n" +
                                                       "```");
            }

            if (args.Length == 0)
            {
                await _WrongSelectionAsync();
                return;
            }
            string action = args[0].ToLower();

            List<string> actionsList = new List<string> { "online", "idle", "dnd", "invisible" };

            if (actionsList.Contains(action))
            {
                if (_client.Status.ToString() == char.ToUpper(action[0]) + action.Substring(1))
                {
                    string _message = "Status doesn't need to be changed.";
                    ManLogger.LogDebug(_message);
                    message.Channel.SendMessageAsync(_message);
                    return;
                }
                else
                {
                    string _message = $"Setting status to {action}.";
                    message.Channel.SendMessageAsync(_message);
                    ManLogger.LogDebug(_message);
                    ManLogger.LogDebug($"Curent Status: {_client.Status.ToString()} | New Status: {char.ToUpper(action[0]) + action.Substring(1)}");

                    await SaveStatus(action);
                    await SetStatus();
                }
            }
            else
            {
                await _WrongSelectionAsync();
            }
        }
        private async Task OwnerActivityAsync(SocketMessage message, string[] args)
        {
            async Task _WrongSelectionAsync()
            {
                await message.Channel.SendMessageAsync("```" +
                                                       "activity [playing/streaming/listening/watching/competing/clear/reset] [Title] [URL] - Set the activity of the bot\n" +
                                                       "```");
            }

            bool isURL(string url)
            {
                Uri uriResult;
                bool isValidUrl = Uri.TryCreate(url, UriKind.Absolute, out uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
                return isValidUrl;
            }
            string RemoveAndSave(List<string> liste)
            {
                if (liste.Count > 0 && isURL(liste.Last()))
                {
                    string url = liste.Last();
                    liste.Remove(url);
                    return url;
                }
                else
                {
                    return null;
                }
            }

            if (args.Length == 0)
            {
                await _WrongSelectionAsync();
                return;
            }

            string action = args[0].ToLower();
            string url = RemoveAndSave(args.ToList()); // Convert array to List<string>
            string title = string.Join(" ", args.Skip(1));
            if (!string.IsNullOrEmpty(url))
            {
                title = title.Replace(url, "").Trim();
            }

            List<string> activityList = new List<string> { "playing", "streaming", "listening", "watching", "competing", "clear", "reset" };
            if (!activityList.Contains(action))
            {
                await _WrongSelectionAsync();
            }
            else
            {
                if (action == "streaming" && string.IsNullOrEmpty(url))
                {
                    await message.Channel.SendMessageAsync("You must provide a URL for streaming.");
                    return;
                }

                try
                {
                    SaveActivity(action.Replace("clear", ""), title != null ? title : "", url != null ? url : "");
                    SetActivity();
                }
                catch (Exception ex)
                {
                    if (ex.Message == "Activity is unchanged.")
                    {
                        message.Channel.SendMessageAsync("Activity doesn't need to be changed.");
                    }
                    return;
                }

                if (action == "reset")
                {
                    SaveActivity("RESET", "TESET", "RESET");
                    SetActivity();
                    return;
                }
                if (action == "clear")
                {
                    string _message = "Activity cleared.";
                    ManLogger.LogDebug(_message);
                    await message.Channel.SendMessageAsync(_message);
                }
                else
                {
                    string _message = $"Setting activity to `{action}` with title `{title}` and url `{url}`.";
                    ManLogger.LogDebug(_message);
                    await message.Channel.SendMessageAsync(_message);
                }
            }
        }

        private Task LogAsync(LogMessage log)
        {
            // Log the message using the provided ILogger instance
            Logger.Log(log.Severity.ToLogLevel(), log.Exception, log.Message);
            return Task.CompletedTask;
        }
        private static LogSeverity GetLogLevel()
        {
            string logLevelString = EnvironmentVariables.LoggingLevel;
            if (Enum.TryParse(logLevelString, true, out LogSeverity logLevel))
            {
                return logLevel;
            }
            else
            {
                return LogSeverity.Warning;
            }
        }


        private async Task OnGuildJoin(SocketGuild guild)
        {
            if (!isInitialized)
            {
                return;
            }
            ManLogger.LogDebug($"I joined guild {guild.Name}.");
        }
        private async Task OnMessage(SocketMessage message)
        {
            if (!isInitialized)
            {
                return;
            }
            ManLogger.LogDebug("Message Received.");
            if (message.Author.IsBot)
            {
                return;
            }
            if (message.Channel is IDMChannel)
            {
                if (message.Author.Id == EnvironmentVariables.OwnerID)
                {
                    async Task _WrongSelectionAsync()
                    {
                        await message.Channel.SendMessageAsync("```" +
                                                               "Commands:\n" +
                                                               "help - Shows this message\n" +
                                                               "log - Get the log\n" +
                                                               "activity - Set the activity of the bot\n" +
                                                               "status - Set the status of the bot\n" +
                                                               "shutdown - Shutdown the bot\n" +
                                                               "```");
                    }
                    ManLogger.LogDebug($"Message received in DM from BotOwner ({message.Author.Username}): {message.Content}");
                    string[] args = message.Content.Split(' ');
                    ManLogger.LogDebug($"Command: {args[0]}");
                    string command = args.First();
                    string[] commandArgs = args.Skip(1).ToArray();

                    switch (command)
                    {
                        case "help":
                            await _WrongSelectionAsync();
                            break;
                        case "log":
                            await OwnerLogAsync(message, commandArgs);
                            break;
                        case "activity":
                            await OwnerActivityAsync(message, commandArgs);
                            break;
                        case "status":
                            await OwnerStatusAsync(message, commandArgs);
                            break;
                        case "shutdown":
                            await ShutdownAsync(message);
                            break;
                        default:
                            await _WrongSelectionAsync();
                            break;
                    }
                }
                else
                {
                    ManLogger.LogDebug($"Message received in DM from {message.Author.Username}: {message.Content}");
                }
            }
            else
            {
                ManLogger.LogDebug($"Message received in Guild from {message.Author.Username}: {message.Content}");
            }
        }
        private async Task OnGuildRemove(SocketGuild guild)
        {
            if (!isInitialized)
            {
                return;
            }
            ManLogger.LogDebug($"I left guild {guild.Name}.");
        }
        private async Task OnReady()
        {
            if (isInitialized)
            {
                return;
            }
            var owner = await _client.GetUserAsync(EnvironmentVariables.OwnerID);
            if (owner is null)
            {
                ManLogger.LogCritical($"Invalid ownerID: {EnvironmentVariables.OwnerID}");
                SentrySdk.CaptureException(new Exception($"Invalid ownerID: {EnvironmentVariables.OwnerID}"));
                Environment.Exit(1);
            }

            await SetStatus();
            await SetActivity();
            isInitialized = true;
            ManLogger.LogDebug($"Bot is ready. Owner: {owner.Username}");
        }
        private async Task OnUserJoin(SocketGuildUser user)
        {
            if (!isInitialized)
            {
                return;
            }
            ManLogger.LogDebug($"{user.GlobalName} joined {user.Guild.Name}.");
        }
        private async Task OnUserLeave(SocketGuild guild, SocketUser user)
        {
            if (!isInitialized)
            {
                return;
            }
            ManLogger.LogDebug($"{user.GlobalName} left {guild.Name}.");
        }
    }
}
