using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rooms.Chat.Filter;
using Plus.HabboHotel.Users.Messenger;

namespace Plus.HabboHotel.Friends;

/// <summary>Delivers messenger output without packet or composer types; the communication layer renders it.</summary>
public interface IMessengerCommunicationOutput
{
    void InstantMessageError(GameClient session, MessengerMessageErrors error, int userId);
    void Notice(GameClient session, string text);
}

public interface IMessengerCommunicationService
{
    Task SendMessage(GameClient session, int friendId, string text);
    Task RequestFriend(GameClient session, string username);
}

public sealed class MessengerCommunicationService(
    IWordFilterManager wordFilter,
    IMessengerDataLoader messengerData,
    IQuestManager quests,
    IRewardTrackManager rewards,
    IMessengerCommunicationOutput output,
    IMessengerFriendMutationService friends,
    IGameClientManager clients) : IMessengerCommunicationService
{
    public async Task SendMessage(GameClient session, int friendId, string text)
    {
        var habbo = session.GetHabbo();
        var messenger = habbo.Messenger;
        var friend = messenger?.GetFriend(friendId);

        if (friend == null) {
            output.InstantMessageError(session, MessengerMessageErrors.NotFriends, friendId);
        }

        var message = wordFilter.CheckMessage(text);

        if (string.IsNullOrWhiteSpace(message)) {
            return;
        }

        if (habbo.TimeMuted > 0) {
            output.Notice(session, "Oops, you're currently muted - you cannot send messages.");

            return;
        }

        // Without a friend there is nothing to deliver to, so the messenger is never asked to send.
        if (messenger == null || friend == null) {
            return;
        }

        var error = messenger.SendMessage(message);

        if (error == MessageError.Flooding) {
            output.Notice(session, "You cannot send a message, you have flooded the console.\n\nYou can send a message in 60 seconds.");
        }

        if (error != null) {
            return;
        }

        await Deliver(session, friend, message);
        rewards.Progress(session, RewardTrackActions.SendMessengerMessage);
    }

    private async Task Deliver(GameClient session, MessengerBuddy friend, string message)
    {
        var senderId = session.GetHabbo().Id;
        await messengerData.LogPrivateMessage(senderId, friend.Id, message);

        if (clients.GetClientByUserId(friend.Id)?.GetHabbo() is not { } target) {
            await messengerData.LogPrivateOfflineMessage(senderId, friend.Id, message);

            return;
        }

        if (target.TimeMuted > 0) {
            output.InstantMessageError(session, MessengerMessageErrors.FriendMuted, friend.Id);

            return;
        }

        if (!target.AllowConsoleMessages || target.IgnoresComponent?.IsIgnored(senderId) == true) {
            output.InstantMessageError(session, MessengerMessageErrors.FriendBusy, friend.Id);

            return;
        }

        if (target.Messenger?.GetFriend(senderId) is { } sender) {
            target.Messenger.ReceiveMessage(sender, message);
        }
    }

    public async Task RequestFriend(GameClient session, string username)
    {
        var (userId, blocked) = await messengerData.CanReceiveFriendRequests(username);

        if (userId == 0 || blocked) {
            return;
        }

        // Accepting is decided under the account holds, so only a newly stored outgoing request earns the reward.
        var outcome = await friends.SendRequestAsync(session.GetHabbo(), userId);

        if (outcome.Error == null && !outcome.Accepted) {
            rewards.Progress(session, RewardTrackActions.RequestFriend);
        }

        // The attempt counts toward the social quest even when the messenger refuses it.
        quests.ProgressUserQuest(session, QuestType.SocialFriend);
    }
}
