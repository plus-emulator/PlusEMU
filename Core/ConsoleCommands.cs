using Microsoft.Extensions.Logging;
using Plus.Communication.Packets.Outgoing.Moderation;
using Plus.HabboHotel.GameClients;

namespace Plus.Core;

public static class ConsoleCommands
{
    private static ILogger? _logger;
    private static ILogger Logger => _logger ?? throw new InvalidOperationException("Configure logging before use.");
    private static IGameClientManager? _clients;
    private static IGameClientManager Clients => _clients ?? throw new InvalidOperationException("Configure console commands before use.");
    private static IPlannedShutdown? _plannedShutdown;

    public static void Configure(ILoggerFactory loggerFactory, IGameClientManager clients, IPlannedShutdown? plannedShutdown = null)
    {
        _logger = loggerFactory.CreateLogger(typeof(ConsoleCommands).FullName!);
        _clients = clients;
        _plannedShutdown = plannedShutdown;
    }

    public static void InvokeCommand(string inputData)
    {
        if (string.IsNullOrEmpty(inputData)) {
            return;
        }

        try {
            var parameters = inputData.Split(' ');

            switch (parameters[0].ToLower()) {
                case "stop":
                case "shutdown": {
                        if (parameters.Length > 1) {
                            SchedulePlannedShutdown(parameters[1..]);
                            break;
                        }

                        Logger.LogWarning("The server is saving users furniture, rooms, etc. WAIT FOR THE SERVER TO CLOSE, DO NOT EXIT THE PROCESS IN TASK MANAGER!!");
                        PlusEnvironment.PerformShutDown();
                        break;
                    }
                case "alert": {
                        var notice = inputData.Substring(6);
                        Clients.SendPacket(new BroadcastMessageAlertComposer($"{PlusEnvironment.LanguageManager.TryGetValue("server.console.alert")}\n\n{notice}"));
                        Logger.LogInformation("Alert successfully sent.");
                        break;
                    }
                default: {
                        Logger.LogError("{Command} is an unknown or unsupported command. Type help for more information", parameters[0].ToLower());
                        break;
                    }
            }
        }
        catch (Exception e) {
            Logger.LogError(e, "Error in command [{Input}]", inputData);
        }
    }

    private static void SchedulePlannedShutdown(string[] arguments)
    {
        if (!PlannedShutdown.TryParse(arguments, out var minutes, out var durationMinutes)) {
            Logger.LogError("Usage: shutdown <minutes> [duration in minutes]");

            return;
        }

        if (_plannedShutdown?.TrySchedule(minutes, durationMinutes) != true) {
            Logger.LogError("The shutdown was not scheduled. The minutes must be 1 or more, and only one shutdown can be pending.");

            return;
        }

        Logger.LogWarning("Every user was warned. The server shuts down in {Minutes} minutes.", minutes);
    }
}
