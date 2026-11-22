using Plus.Communication.RCON.Commands;

namespace Plus.Communication.RCON;

public interface IRconSocket
{
    void Init(RconConfiguration configuration);
    ICommandManager GetCommands();
}
