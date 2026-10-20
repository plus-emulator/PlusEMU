using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Collectibles;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

internal class GiveCollectibleCommand(INftWardrobeService collectibles) : ITargetChatCommand
{
    public string Key => "givecollectible";

    public string Parameters => "%username% %collection%";

    public string Description => "Give another user a collectible avatar of your current look (collection: avatar, clothes or genesis).";

    public bool MustBeInSameRoom => false;

    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters) => throw new NotImplementedException();
}
