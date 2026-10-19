using System.Collections.Immutable;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Avatar;

/// <summary>One collectible avatar of the avatar editor's Avatar Collectibles tab.</summary>
public sealed record NftWardrobeItem(string Id, string Figure, string Gender, string TokenId, string ContractKey);

/// <summary>AIR package_213/class_4188.as:23 and NftWardrobeItem.as:20: count, then id, figure, gender, token id and contract key per item.</summary>
public class UserNftWardrobeComposer(ImmutableArray<NftWardrobeItem> items) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.UserNftWardrobeComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(items.Length);

        foreach (var item in items) {
            packet.WriteString(item.Id);
            packet.WriteString(item.Figure);
            packet.WriteString(item.Gender);
            packet.WriteString(item.TokenId);
            packet.WriteString(item.ContractKey);
        }
    }
}
