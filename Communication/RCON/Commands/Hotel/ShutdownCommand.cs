using Plus.Core;

namespace Plus.Communication.RCON.Commands.Hotel;

internal class ShutdownCommand(IPlannedShutdown plannedShutdown) : IRconCommand
{
    public string Key => "shutdown";
    public string Parameters => "%minutes% %duration%";
    public string Description => "This command warns every user and shuts the server down after the given minutes.";

    public Task<bool> TryExecute(string[] parameters) => throw new NotImplementedException();
}
