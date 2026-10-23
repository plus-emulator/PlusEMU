using Plus.Communication.Packets;
using System.Collections.Immutable;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Outgoing.Rooms.Engine;

/// <summary>
/// Habbo (1): sex, group id, group status, group name,
/// swim figure, achievement score, moderator, badges rank. Pet (2): type, owner id, owner name, rarity, saddle, riding, can breed,
/// can harvest, can revive, breeding permission, level, posture. Rentable bot (4): sex, owner id, owner name, skills. Nothing follows.
/// </summary>
public sealed class UsersComposer : IServerPacket
{
    /// <summary>No rank. The client defaults to it and hides a negative rank.</summary>
    private const int BadgesRankUnknown = -1;
    /// <summary>The skills of a bot type without a list of its own.</summary>
    private static readonly short[] DefaultBotSkills = [1, 2, 3, 4, 5];

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
                var skills = user.BotSkills ?? DefaultBotSkills;
                packet.WriteInteger(skills.Count);

                foreach (var skill in skills) {
                    packet.WriteShort(skill);
                }
            }
        }
    }
}
