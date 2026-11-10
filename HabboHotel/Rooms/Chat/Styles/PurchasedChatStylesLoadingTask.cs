using Dapper;
using Plus.Database;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.UserData;

namespace Plus.HabboHotel.Rooms.Chat.Styles;

internal sealed class PurchasedChatStylesLoadingTask(IDatabase database) : IUserDataLoadingTask
{
    public async Task Load(Habbo habbo)
    {
        using var connection = database.Connection();
        var ids = await connection.QueryAsync<int>("SELECT style_id FROM user_purchased_chat_styles WHERE user_id=@id", new { id = habbo.Id });

        foreach (var id in ids) {
            habbo.PurchasedChatStyles.TryAdd(id, 0);
        }
    }
}
