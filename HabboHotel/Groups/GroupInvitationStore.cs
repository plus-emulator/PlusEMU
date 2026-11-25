using Dapper;
using Plus.Database;
using Plus.Utilities.DependencyInjection;

namespace Plus.HabboHotel.Groups;

public sealed record GroupInvitation(int GroupId, int UserId, int InviterId, uint RoomId, DateTimeOffset CreatedAt);

[Singleton]
public interface IGroupInvitationStore
{
    /// <summary>Stores the invitation, or refreshes the inviter, room and time of the pending one.</summary>
    void Save(GroupInvitation invitation);
    GroupInvitation? Find(int groupId, int userId);
    bool Delete(int groupId, int userId);

    /// <summary>Turns a pending invitation into a membership in one transaction; without one nothing changes.</summary>
    GroupJoinOutcome Accept(int userId, int groupId, int membershipLimit);
}

public sealed class GroupInvitationStore(IDatabase database) : IGroupInvitationStore
{
    public void Save(GroupInvitation invitation)
    {
        using var connection = database.Connection();
        connection.Execute(
            "INSERT INTO group_invitations (group_id, user_id, inviter_id, room_id, created_at) VALUES (@GroupId, @UserId, @InviterId, @RoomId, @CreatedAt) " +
            "ON DUPLICATE KEY UPDATE inviter_id = VALUES(inviter_id), room_id = VALUES(room_id), created_at = VALUES(created_at)",
            new { invitation.GroupId, invitation.UserId, invitation.InviterId, invitation.RoomId, CreatedAt = invitation.CreatedAt.UtcDateTime });
    }

    public GroupInvitation? Find(int groupId, int userId)
    {
        using var connection = database.Connection();
        var row = connection.QuerySingleOrDefault<InvitationRow>(
            "SELECT group_id, user_id, inviter_id, room_id, created_at FROM group_invitations WHERE group_id = @groupId AND user_id = @userId",
            new { groupId, userId });

        return row == null
            ? null
            : new(row.GroupId, row.UserId, row.InviterId, row.RoomId, new DateTimeOffset(DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc)));
    }

    public bool Delete(int groupId, int userId)
    {
        using var connection = database.Connection();

        return connection.Execute("DELETE FROM group_invitations WHERE group_id = @groupId AND user_id = @userId", new { groupId, userId }) > 0;
    }

    // Locks the account and group rows in the same order as a normal join, so both paths count memberships under one lock.
    public GroupJoinOutcome Accept(int userId, int groupId, int membershipLimit)
    {
        using var connection = database.Connection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        if (!GroupParticipationStore.LockAccount(connection, transaction, userId) || GroupParticipationStore.LockGroup(connection, transaction, groupId) is null) {
            return GroupJoinOutcome.Refused;
        }

        var key = new { userId, groupId };

        if (connection.Query<int>("SELECT 1 FROM group_invitations WHERE group_id = @groupId AND user_id = @userId FOR UPDATE", key, transaction).Count() != 1) {
            return GroupJoinOutcome.Refused;
        }

        if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM group_memberships WHERE user_id = @userId AND group_id = @groupId", key, transaction) > 0) {
            connection.Execute("DELETE FROM group_invitations WHERE group_id = @groupId AND user_id = @userId", key, transaction);
            transaction.Commit();

            return GroupJoinOutcome.AlreadyPresent;
        }

        var memberships = connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM group_memberships AS m INNER JOIN `groups` AS g ON m.group_id = g.id WHERE m.user_id = @userId", key, transaction);

        if (memberships >= membershipLimit) {
            return GroupJoinOutcome.LimitReached;
        }

        connection.Execute("INSERT INTO group_memberships (user_id, group_id, `rank`) VALUES (@userId, @groupId, 0)", key, transaction);
        connection.Execute("DELETE FROM group_requests WHERE user_id = @userId AND group_id = @groupId", key, transaction);
        connection.Execute("DELETE FROM group_invitations WHERE group_id = @groupId AND user_id = @userId", key, transaction);
        transaction.Commit();

        return GroupJoinOutcome.Inserted;
    }

    private sealed class InvitationRow
    {
        public int GroupId { get; set; }
        public int UserId { get; set; }
        public int InviterId { get; set; }
        public uint RoomId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
