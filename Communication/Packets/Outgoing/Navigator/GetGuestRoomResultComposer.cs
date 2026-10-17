using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Outgoing.Navigator;

public sealed class GetGuestRoomResultComposer(GuestRoomInfoSnapshot snapshot) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.GetGuestRoomResultComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteBoolean(snapshot.IsLoading);
        packet.WriteUInteger(snapshot.Id);
        packet.WriteString(snapshot.Name);
        packet.WriteInteger(snapshot.OwnerId);
        packet.WriteString(snapshot.OwnerName);
        packet.WriteInteger(snapshot.Access);
        packet.WriteInteger(snapshot.UsersNow);
        packet.WriteInteger(snapshot.UsersMax);
        packet.WriteString(snapshot.Description);
        packet.WriteInteger(snapshot.TradeSettings);
        packet.WriteInteger(snapshot.Score);
        packet.WriteInteger(0); //Top rated room rank.
        packet.WriteInteger(snapshot.Category);
        packet.WriteInteger(snapshot.Tags.Length);

        foreach (var tag in snapshot.Tags) {
            packet.WriteString(tag);
        }

        // AIR WIN63-202610151745-318700621 package_52/class_2175.as:103: bit 2 adds the group, bit 4 the promotion;
        // 8 | 16 | 32 are the show-owner, allow-pets and entry-ad flags.
        var flags = 56 | (snapshot.Group != null ? 2 : 0) | (snapshot.Promotion != null ? 4 : 0);
        packet.WriteInteger(flags);

        if (snapshot.Group != null) {
            packet.WriteInteger(snapshot.Group.Id);
            packet.WriteString(snapshot.Group.Name);
            packet.WriteString(snapshot.Group.Badge);
        }

        if (snapshot.Promotion != null) {
            packet.WriteString(snapshot.Promotion.Name);
            packet.WriteString(snapshot.Promotion.Description);
            packet.WriteInteger(snapshot.Promotion.MinutesLeft);
        }

        packet.WriteBoolean(snapshot.CheckEntry);
        packet.WriteBoolean(false);
        packet.WriteBoolean(false);
        packet.WriteBoolean(false);
        packet.WriteInteger(snapshot.WhoCanMute);
        packet.WriteInteger(snapshot.WhoCanKick);
        packet.WriteInteger(snapshot.WhoCanBan);
        packet.WriteBoolean(snapshot.CanModify);
        // AIR WIN63-202610151745-318700621 package_60/class_2076.as:40 reads only the flood sensitivity as chat
        // settings, then one more boolean that has no counterpart here.
        packet.WriteInteger(snapshot.ExtraFlood);
        packet.WriteBoolean(false);
    }
}
