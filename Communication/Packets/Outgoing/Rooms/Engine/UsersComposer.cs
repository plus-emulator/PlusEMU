using Plus.Communication.Packets;
using System.Collections.Immutable;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Outgoing.Rooms.Engine;

/// <summary>
/// class_2126 -> class_2291 (WIN63-202610131318-860371703, parse L72-186). Habbo (1): sex, group id, group status, group name,
/// swim figure, achievement score, moderator, badges rank. Pet (2): type, owner id, owner name, rarity, saddle, riding, can breed,
/// can harvest, can revive, breeding permission, level, posture. Rentable bot (4): sex, owner id, owner name, skills. Nothing follows.
/// </summary>
public sealed class UsersComposer : IServerPacket
{
    /// <summary>No rank; class_2587.flush L63 defaults to it and a negative rank is hidden.</summary>
    private const int BadgesRankUnknown = -1;

    private readonly ImmutableArray<RoomUserSnapshot> _users;

    public UsersComposer(RoomUserSnapshot user) : this([user]) { }
    public UsersComposer(IReadOnlyList<RoomUserSnapshot> users) => _users = users.ToImmutableArray();
    public uint MessageId => ServerPacketHeader.UsersComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_users.Length);

        foreach (var user in _users) {
            packet.WriteInteger(user.Id);
            packet.WriteString(user.Name);
            packet.WriteString(user.Motto);
            packet.WriteString(user.Look);
            packet.WriteInteger(user.VirtualId);
            packet.WriteInteger(user.X);
            packet.WriteInteger(user.Y);
            packet.WriteString(user.Z);
            packet.WriteInteger(user.Rotation);
            packet.WriteInteger(user.Kind);

            if (user.Kind == 1) {
                packet.WriteString(user.Gender);
                packet.WriteInteger(user.GroupId);
                packet.WriteInteger(0);
                packet.WriteString(user.GroupName);
                packet.WriteString("");
                packet.WriteInteger(user.AchievementPoints);
                packet.WriteBoolean(false);
                packet.WriteInteger(BadgesRankUnknown);
            }
            else if (user.IsPet) {
                packet.WriteInteger(user.PetType);
                packet.WriteInteger(user.OwnerId);
                packet.WriteString(user.OwnerName);
                packet.WriteInteger(1);
                packet.WriteBoolean(user.HasSaddle);
                packet.WriteBoolean(user.IsRidden);
                packet.WriteBoolean(false);
                packet.WriteBoolean(false);
                packet.WriteBoolean(false);
                packet.WriteBoolean(false);
                packet.WriteInteger(user.PetLevel);
                packet.WriteString("");
            }
            else {
                packet.WriteString(user.Gender);
                packet.WriteInteger(user.OwnerId);
                packet.WriteString(user.OwnerName);
                packet.WriteInteger(5);
                packet.WriteShort(1);
                packet.WriteShort(2);
                packet.WriteShort(3);
                packet.WriteShort(4);
                packet.WriteShort(5);
            }
        }
    }
}
