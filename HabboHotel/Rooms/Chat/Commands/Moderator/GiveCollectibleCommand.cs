using System.Collections.Immutable;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Collectibles;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

internal class GiveCollectibleCommand(INftWardrobeService collectibles) : ITargetChatCommand
{
    private static readonly ImmutableDictionary<string, string> ContractKeys = new Dictionary<string, string>
    {
        ["avatar"] = "habbo:avatar",
        ["clothes"] = "habbo:clothes",
        ["genesis"] = "habbo:avatar_genesis",
    }.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);

    public string Key => "givecollectible";

    public string Parameters => "%username% %collection%";

    public string Description => "Give another user a collectible avatar of your current look (collection: avatar, clothes or genesis).";

    public bool MustBeInSameRoom => false;

    public async Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        var actor = session.GetHabbo();

        if (target.Id != actor.Id && !actor.Access.Outranks(target.Access)) {
            return;
        }

        if (!ContractKeys.TryGetValue(parameters.FirstOrDefault() ?? "avatar", out var contractKey)) {
            session.SendWhisper("Usage: :givecollectible <username> [avatar|clothes|genesis]");

            return;
        }

        await collectibles.Give(target, actor.Look, actor.Gender.ToUpper(), contractKey);

        if (target.Id != actor.Id) {
            target.Client?.SendNotification("You have just been given a collectible avatar!");
        }

        session.SendWhisper($"You gave {target.Username} a collectible avatar of your look.");
    }
}
