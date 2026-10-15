using Plus.Core.Settings;
using Plus.HabboHotel.Rooms.AI;
using Plus.HabboHotel.Rooms.PathFinding;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    [Fact]
    public void ExecutorPetMoveAndRotateSnapOnTheRoomTick()
    {
        ExecutorActor(0, 1);
        var horse = ExternalLifecycleHorse(2, 1);
        ExecutorTick();
        var store = new UnitMovePetStore();
        var pets = new PetPlacementService(store, TestGameClientManager.Empty, Proxy<ISettingsManager>((_, _) => null));

        pets.Move(_room, _client, 50, 3, 2, 5);

        // The room owner checks, stores and snaps on its next tick.
        Assert.Empty(store.Moves);
        Assert.Equal((2, 1), (horse.X, horse.Y));
        ExecutorTick();
        Assert.Equal((3, 2), (Assert.Single(store.Moves).X, store.Moves[0].Y));
        Assert.Equal((3, 2, 0d, 5, 5), (horse.X, horse.Y, horse.Z, horse.RotHead, horse.RotBody));
        Assert.Equal(0, horse.Movement.PendingCount);
        Assert.Contains(horse, _room.GetGameMap().GetRoomUsers(new(3, 2)));
        Assert.DoesNotContain(horse, _room.GetGameMap().GetRoomUsers(new(2, 1)));
        var moved = ExecutorUpdate(horse);
        Assert.Equal((3, 2), (moved.X, moved.Y));
        Assert.DoesNotContain("/mv ", moved.Status);

        pets.Move(_room, _client, 50, 3, 2, 1);
        ExecutorTick();

        Assert.Single(store.Moves);
        Assert.Equal((3, 2, 1, 1), (horse.X, horse.Y, horse.RotHead, horse.RotBody));
    }

    [Fact]
    public void ExecutorBotMoveIsDroppedUnstoredWhenTheTileIsTakenBeforeTheTick()
    {
        var actor = ExecutorActor(0, 1);
        var bot = ExecutorAdditionalBot(2, 1, 3);
        ExecutorTick();
        var store = new UnitMoveBotStore();
        var bots = new BotManagementService(store, null!);

        bots.Move(_room, _client, bot.VirtualId, 3, 1, 6);
        ExecutorTick();

        Assert.Equal((3, 1, 6, 6), (bot.X, bot.Y, bot.RotHead, bot.RotBody));
        Assert.Equal((3, 1, 6), (bot.BotData!.X, bot.BotData.Y, bot.BotData.Rot));
        Assert.Contains(bot, _room.GetGameMap().GetRoomUsers(new(3, 1)));

        // The avatar's placement is queued first, so on the tick the bot finds the tile taken.
        _room.GetGameMap().Navigation!.ForcePlace(actor, 3, 3, 0, ForceResolution.Highest);
        bots.Move(_room, _client, bot.VirtualId, 3, 3, 2);
        ExecutorTick();

        Assert.Equal((3, 3), (actor.X, actor.Y));
        Assert.Equal((3, 1, 6), (bot.X, bot.Y, bot.RotBody));
        Assert.Equal((3, 1, 6), (bot.BotData.X, bot.BotData.Y, bot.BotData.Rot));
        Assert.Equal(1, store.Saved);
    }

    private sealed class UnitMovePetStore : IPetRoomStore
    {
        public List<PetRoomMove> Moves { get; } = [];
        public bool TryMove(PetRoomMove move)
        {
            Moves.Add(move);

            return true;
        }
    }

    private sealed class UnitMoveBotStore : IBotManagementStore
    {
        public int Saved { get; private set; }
        public void SavePosition(int botId, uint roomId, int x, int y, double z, int rotation) => Saved++;
        public BotPlacementData Place(int botId, int ownerId, uint roomId, int x, int y) => throw new NotSupportedException();
        public void PickUp(int botId, uint roomId) => throw new NotSupportedException();
        public void SaveAppearance(int botId, uint roomId, string look, string gender) => throw new NotSupportedException();
        public IReadOnlyList<string> SaveSpeech(int botId, uint roomId, IReadOnlyList<string> speech, bool automatic, int interval, bool mix) =>
            throw new NotSupportedException();
        public void SaveWalkingMode(int botId, uint roomId, string mode) => throw new NotSupportedException();
        public void SaveName(int botId, uint roomId, string name) => throw new NotSupportedException();
    }
}
