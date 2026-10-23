using Plus.HabboHotel.Users.Inventory.Bots;
using System.Text.RegularExpressions;
using Dapper;
using Plus.Communication.Packets.Outgoing.Inventory.Bots;
using Plus.Communication.Packets.Outgoing.Rooms.AI.Bots;
using Plus.Communication.Packets.Outgoing.Rooms.Avatar;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.Core.FigureData;
using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Rooms.AI.Speech;
using Plus.HabboHotel.Subscriptions;

using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Rooms.AI;

public enum BotAction
{
    CopyLooks = 1, Speech = 2, Relax = 3, Dance = 4, Rename = 5
}
public sealed record BotActionRequest(int BotId, BotAction Action, string Data);
public readonly record struct BotActionViewSnapshot(int BotId, int ActionId, string Data);
public sealed record BotPlacementData(string AiType, string WalkMode, bool AutomaticChat, int SpeakingInterval, bool MixSentences, int ChatBubble,
    IReadOnlyList<string> Speech);

public interface IBotManagementStore
{
    BotPlacementData Place(int botId, int ownerId, uint roomId, int x, int y);
    void PickUp(int botId, uint roomId);
    void SaveAppearance(int botId, uint roomId, string look, string gender);
    IReadOnlyList<string> SaveSpeech(int botId, uint roomId, IReadOnlyList<string> speech, bool automatic, int interval, bool mix);
    void SaveWalkingMode(int botId, uint roomId, string mode);
    void SaveName(int botId, uint roomId, string name);
    void SavePosition(int botId, uint roomId, int x, int y, double z, int rotation);
}

public sealed class BotManagementStore(IDatabase database) : IBotManagementStore
{
    public BotPlacementData Place(int botId, int ownerId, uint roomId, int x, int y)
    {
        using var connection = database.Connection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        if (connection.Execute("UPDATE bots SET room_id=@roomId,x=@x,y=@y WHERE id=@botId AND user_id=@ownerId AND room_id=0 LIMIT 1",
                new { roomId, x, y, botId, ownerId }, transaction) != 1) {
            throw new InvalidOperationException("Bot placement was not persisted.");
        }

        var row = connection.QuerySingle<PlacementRow>(
            "SELECT ai_type AS AiType,walk_mode AS WalkMode,automatic_chat AS AutomaticChat,speaking_interval AS SpeakingInterval,mix_sentences AS MixSentences,chat_bubble AS ChatBubble FROM bots WHERE id=@botId LIMIT 1",
            new { botId }, transaction);
        var speech = connection.Query<string>("SELECT text FROM bots_speech WHERE bot_id=@botId", new { botId }, transaction).ToArray();
        transaction.Commit();

        return new(row.AiType, row.WalkMode, row.AutomaticChat, row.SpeakingInterval, row.MixSentences, row.ChatBubble, speech);
    }

    public void PickUp(int botId, uint roomId) => Execute("UPDATE bots SET room_id=0 WHERE id=@botId AND room_id=@roomId LIMIT 1", new { botId, roomId });
    public void SaveAppearance(int botId, uint roomId, string look, string gender) => Execute("UPDATE bots SET look=@look,gender=@gender WHERE id=@botId AND room_id=@roomId LIMIT 1", new { botId, roomId, look, gender });
    public void SaveWalkingMode(int botId, uint roomId, string mode) => Execute("UPDATE bots SET walk_mode=@mode WHERE id=@botId AND room_id=@roomId LIMIT 1", new { botId, roomId, mode });
    public void SaveName(int botId, uint roomId, string name) => Execute("UPDATE bots SET name=@name WHERE id=@botId AND room_id=@roomId LIMIT 1", new { botId, roomId, name });
    public void SavePosition(int botId, uint roomId, int x, int y, double z, int rotation) => Execute("UPDATE bots SET x=@x,y=@y,z=@z,rotation=@rotation WHERE id=@botId AND room_id=@roomId LIMIT 1", new { botId, roomId, x, y, z, rotation });

    public IReadOnlyList<string> SaveSpeech(int botId, uint roomId, IReadOnlyList<string> speech, bool automatic, int interval, bool mix)
    {
        using var connection = database.Connection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        connection.Execute("DELETE FROM bots_speech WHERE bot_id=@botId", new { botId }, transaction);

        foreach (var text in speech) {
            connection.Execute("INSERT INTO bots_speech (bot_id,text) VALUES (@botId,@text)", new { botId, text }, transaction);
        }

        if (connection.Execute("UPDATE bots SET automatic_chat=@automatic,speaking_interval=@interval,mix_sentences=@mix WHERE id=@botId AND room_id=@roomId LIMIT 1",
                new { botId, roomId, automatic = automatic ? "true" : "false", interval, mix }, transaction) != 1) {
            throw new InvalidOperationException("Bot speech was not persisted.");
        }

        transaction.Commit();

        return speech;
    }

    private void Execute(string sql, object args)
    {
        using var connection = database.Connection();

        if (connection.Execute(sql, args) != 1) {
            throw new InvalidOperationException("Bot mutation was not persisted.");
        }
    }
    private sealed class PlacementRow
    {
        public string AiType { get; set; } = "";
        public string WalkMode { get; set; } = "";
        public bool AutomaticChat { get; set; }
        public int SpeakingInterval { get; set; }
        public bool MixSentences { get; set; }
        public int ChatBubble { get; set; }
    }
}

public interface IBotManagementService
{
    void Place(Room room, GameClient session, int botId, int x, int y);
    void PickUp(GameClient session, int botId);
    void Move(Room room, GameClient session, int virtualId, int x, int y, int direction);
    void SaveAction(GameClient session, BotActionRequest request);
    void ShowAction(GameClient session, int botId, int actionId);
}

public sealed class BotManagementService(IBotManagementStore store, IFigureDataManager figures, Random? random = null) : IBotManagementService
{
    private readonly Random _random = random ?? Random.Shared;

    /// <summary>A bot placed from the inventory faces direction 2.</summary>
    private const int PlacedBotRotation = 2;

    /// <summary>A placed bot says one of these lines, picked at random.</summary>
    private static readonly string[] PlacementGreetings = ["Hello!", "Yo!", "Hello I'm a real party animal!"];

    public void ShowAction(GameClient session, int botId, int actionId)
    {
        var room = session.GetHabbo().CurrentRoom;

        if (room == null || actionId is not (2 or 5) ||
            !room.GetRoomUserManager().TryGetBot(botId, out var user) || user.BotData is not { IsTemporary: false } bot) {
            return;
        }

        var data = actionId == 5 ? bot.Name :
            string.Concat(bot.RandomSpeech.Select(speech => speech.Message + "\n")) + ";#;" +
            bot.AutomaticChat + ";#;" + bot.SpeakingInterval.ToString(System.Globalization.CultureInfo.InvariantCulture) + ";#;" + bot.MixSentences;
        session.Send(new OpenBotActionComposer(new(bot.Id, actionId, data)));
    }

    public void Place(Room room, GameClient session, int botId, int x, int y)
    {
        if (!room.CheckRights(session, true)) {
            return;
        }

        if (!room.GetGameMap().CanWalk(x, y, false) || !room.GetGameMap().ValidTile(x, y)) {
            session.SendNotification("You cannot place a bot here!");

            return;
        }

        if (session.GetHabbo().Inventory is not { } inventory || !inventory.Bots.Bots.TryGetValue(botId, out var bot)) {
            return;
        }

        var count = room.GetRoomUserManager().GetUserList().Count(user => user != null && user.IsBot && !user.IsPet);

        if (count >= 5 && !session.GetHabbo().Access.Can(PermissionKeys.BotPlaceAnyOverride)) {
            session.SendNotification("Sorry; 5 bots per room only!");

            return;
        }

        var data = store.Place(bot.Id, bot.OwnerId, room.RoomId, x, y);
        var speeches = data.Speech.Select(text => new RandomSpeech(text, bot.Id)).ToList();
        // A placed bot shows its motto and gender ("m") in Users and UserChange.
        var botUser = room.GetRoomUserManager().DeployBot(new(bot.Id, room.RoomId, data.AiType, data.WalkMode, bot.Name, bot.Motto ?? "", bot.Figure, x, y, room.GetGameMap().SqAbsoluteHeight(x, y),
            PlacedBotRotation, 0, 0, 0, 0, ref speeches, string.IsNullOrEmpty(bot.Gender) ? "M" : bot.Gender, 0, bot.OwnerId, data.AutomaticChat, data.SpeakingInterval,
            data.MixSentences, data.ChatBubble), null);
        botUser.Chat(PlacementGreetings[_random.Next(PlacementGreetings.Length)], data.ChatBubble);
        room.GetGameMap().UpdateUserMovement(new(x, y), new(x, y), botUser);

        if (inventory.Bots.RemoveBot(botId)) {
            session.Send(new BotInventoryComposer(BotInventorySnapshot.Capture(inventory.Bots.Bots.Values)));
        }
    }

    public void PickUp(GameClient session, int botId)
    {
        var habbo = session.GetHabbo();

        // A picked-up bot goes into the loaded inventory, so nothing is picked up without one.
        if (!habbo.InRoom || botId == 0 || habbo.CurrentRoom == null || habbo.Inventory is not { } inventory) {
            return;
        }

        var room = habbo.CurrentRoom;

        if (!room.GetRoomUserManager().TryGetBot(botId, out var bot) || bot.BotData is not { IsTemporary: false } botData) {
            return;
        }

        if (habbo.Id != botData.OwnerId && !habbo.Access.Can(PermissionKeys.BotPlaceAnyOverride)) {
            session.SendWhisper("You can only pick up your own bots!");

            return;
        }

        store.PickUp(botId, room.RoomId);
        room.GetGameMap().RemoveUserFromMap(bot, new(bot.X, bot.Y));
        inventory.Bots.AddBot(new(botData.Id, botData.OwnerId, botData.Name, botData.Motto, botData.Look, botData.Gender));
        session.Send(new BotInventoryComposer(BotInventorySnapshot.Capture(inventory.Bots.Bots.Values)));
        room.GetRoomUserManager().RemoveBot(bot.VirtualId, false);
    }

    // A room controller may move (Alt+click, then a tile) or rotate (Shift+click) a placed rentable bot.
    // It names the bot by its room index, so avatars and pets sharing that index space are refused.
    public void Move(Room room, GameClient session, int virtualId, int x, int y, int direction)
    {
        var habbo = session.GetHabbo();

        if (!ReferenceEquals(habbo.CurrentRoom, room) || direction < 0
            || room.GetRoomUserManager().GetRoomUserByVirtualId(virtualId) is not { IsBot: true, IsPet: false } unit
            || unit.BotData is not { IsTemporary: false } bot) {
            return;
        }

        if (habbo.Id != bot.OwnerId && !room.CheckRights(session, false, true) && !habbo.Access.Can(PermissionKeys.BotPlaceAnyOverride)) {
            return;
        }

        var rotation = direction % 8;
        RoomUnitPlacement.Place(room, unit, x, y, rotation, z =>
        {
            store.SavePosition(bot.Id, room.RoomId, x, y, z, rotation);
            // Room unload saves the live tile and this facing again, so both must match the stored row.
            bot.X = x;
            bot.Y = y;
            bot.Z = z;
            bot.Rot = rotation;

            return true;
        });
    }

    public void SaveAction(GameClient session, BotActionRequest request)
    {
        var habbo = session.GetHabbo();

        if (!habbo.InRoom || habbo.CurrentRoom == null || request.Action is < BotAction.CopyLooks or > BotAction.Rename) {
            return;
        }

        var room = habbo.CurrentRoom;

        if (!room.GetRoomUserManager().TryGetBot(request.BotId, out var bot) || bot.BotData is not { IsTemporary: false } botData) {
            return;
        }

        if (botData.OwnerId != habbo.Id && !habbo.Access.Can(PermissionKeys.BotEditAnyOverride)) {
            return;
        }

        switch (request.Action) {
            case BotAction.CopyLooks:
                // Paid clothing is only checked against a loaded wardrobe; without one the bot keeps its look.
                if (habbo.Clothing is not { } wardrobe) {
                    break;
                }

                var look = figures.ProcessFigure(habbo.Look, habbo.Gender, wardrobe.GetClothingParts, ClubAccess.LevelFor(habbo.Access));
                store.SaveAppearance(botData.Id, room.RoomId, look, habbo.Gender);
                botData.Look = look;
                botData.Gender = habbo.Gender;
                room.SendPacket(new UserChangeComposer(AvatarChangeSnapshot.Capture(botData)));
                break;
            case BotAction.Speech:
                SaveSpeech(botData, request.Data);
                break;
            case BotAction.Relax:
                var mode = botData.WalkingMode == "stand" ? "freeroam" : "stand";
                store.SaveWalkingMode(botData.Id, room.RoomId, mode);
                botData.WalkingMode = mode;
                break;
            case BotAction.Dance:
                botData.DanceId = botData.DanceId > 0 ? 0 : _random.Next(1, 4);
                room.SendPacket(new DanceComposer(bot.VirtualId, botData.DanceId));
                break;
            case BotAction.Rename:
                if (!ValidName(session, request.Data)) {
                    return;
                }

                store.SaveName(botData.Id, room.RoomId, request.Data);
                botData.Name = request.Data;
                room.SendUser(bot);
                break;
        }
    }

    private void SaveSpeech(RoomBot bot, string raw)
    {
        var parts = raw.Split(";#;", StringSplitOptions.None);

        if (parts.Length != 4 || !bool.TryParse(parts[1], out var automatic) ||
            !int.TryParse(parts[2], out var interval) || !bool.TryParse(parts[3], out var mix)) {
            return;
        }

        var speech = parts[0].Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(text => Regex.Replace(text, "<(.|\\n)*?>", string.Empty)).ToArray();
        interval = Math.Max(7, interval);
        var saved = store.SaveSpeech(bot.Id, bot.RoomId, speech, automatic, interval, mix);
        bot.AutomaticChat = automatic;
        bot.SpeakingInterval = interval;
        bot.MixSentences = mix;
        bot.RandomSpeech.Clear();

        foreach (var text in saved) {
            bot.RandomSpeech.Add(new(text, bot.Id));
        }
    }

    private static bool ValidName(GameClient session, string name)
    {
        if (name.Length == 0) {
            session.SendWhisper("Come on, atleast give the bot a name!");

            return false;
        }

        if (name.Length >= 16) {
            session.SendWhisper("Come on, the bot doesn't need a name that long!");

            return false;
        }

        if (name.Contains("<img src") || name.Contains("<font ") || name.Contains("</font>") || name.Contains("</a>") || name.Contains("<i>")) {
            session.SendWhisper("No HTML, please :<");

            return false;
        }

        return true;
    }
}
