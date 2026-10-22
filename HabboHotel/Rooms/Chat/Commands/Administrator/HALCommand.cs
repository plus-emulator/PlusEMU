using Plus.Communication.Packets.Outgoing.Moderation;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Administrator;

internal class HalCommand : IChatCommand
{
    private readonly IGameClientManager _clientManager;
    public string Key => "hal";

    public string Parameters => "%link% %message%";

    public string Description => "Send a message to the entire hotel, with a link.";

    public HalCommand(IGameClientManager clientManager)
    {
        _clientManager = clientManager;
    }

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        if (parameters.Length <= 1 || !IsWebLink(parameters[0])) {
            session.SendWhisper("Please enter a link and a message: :hal %link% %message%. Use :ha for an alert without a link.");

            return;
        }

        var url = parameters[0];
        var message = CommandManager.MergeParams(parameters, 1);
        _clientManager.SendPacket(new ModeratorMessageComposer($"{message}\r\n- {session.GetHabbo().Username}", url));
    }

    private static bool IsWebLink(string value) => Uri.TryCreate(value, UriKind.Absolute, out var link)
        && (link.Scheme == Uri.UriSchemeHttp || link.Scheme == Uri.UriSchemeHttps);
}
