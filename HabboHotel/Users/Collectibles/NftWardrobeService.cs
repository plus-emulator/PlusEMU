using System.Collections.Immutable;
using Plus.Communication.Packets.Outgoing.Avatar;
using Plus.Database;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Users.Collectibles;

/// <summary>The collectible worn now: its token and the look it replaced.</summary>
public sealed record WornCollectible(string TokenId, string FallbackFigure, string FallbackGender);

/// <summary>The hotel's avatar collectibles: looks staff give to a user, listed in the avatar editor's Avatar Collectibles tab.</summary>
public interface INftWardrobeService
{
    Task<ImmutableArray<NftWardrobeItem>> List(GameClient session);
    Task<WornCollectible?> Worn(int userId);
    Task Wear(GameClient session, string id);
    Task<NftWardrobeItem?> Give(Habbo target, string figure, string gender, string contractKey);
}

public sealed class NftWardrobeService(IDatabase database) : INftWardrobeService
{
    public Task<ImmutableArray<NftWardrobeItem>> List(GameClient session) => throw new NotImplementedException();

    public Task<WornCollectible?> Worn(int userId) => throw new NotImplementedException();

    public Task Wear(GameClient session, string id) => throw new NotImplementedException();

    public Task<NftWardrobeItem?> Give(Habbo target, string figure, string gender, string contractKey) => throw new NotImplementedException();
}
