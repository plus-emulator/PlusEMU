using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Users;

/// <summary>The ignored user ids.</summary>
public class IgnoredUsersComposer : IServerPacket
{
    private readonly IReadOnlyCollection<int> _ignoredUsers;

    public uint MessageId => ServerPacketHeader.IgnoredUsersComposer;

    public IgnoredUsersComposer(IEnumerable<int> ignoredUsers)
    {
        _ignoredUsers = ignoredUsers.ToArray();
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_ignoredUsers.Count);

        foreach (var userId in _ignoredUsers) {
            packet.WriteInteger(userId);
        }
    }
}
