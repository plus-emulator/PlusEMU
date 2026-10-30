using System.Buffers.Binary;
using System.Reflection;
using Plus.Communication.Packets.Incoming.Rooms.Avatar;
using Plus.Communication.Packets.Incoming.Rooms.Engine;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Modern;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Instance;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private static WiredEntry[] AvatarEntries(IEnumerable<(uint Header, byte[] Body)> sent) =>
        [.. WiredMovementPackets(sent).SelectMany(packet => packet).Where(entry => entry.Type == 0)];

    // A raise that runs one pulse or more after the move is a new wave: the user the furni was moved onto is lifted with it.
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    public async Task ADelayedRaiseAfterTheFurniWasMovedOntoAUserLiftsTheUser(bool noAnimation, int pulses)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: true, turn: false, altitudeDelay: pulses);
        tile.Definition.Walkable = true;
        await WalkTo(actor, 0, 3);

        var sent = await ClickABot();

        Assert.Equal((0, 3, 0.3), (tile.GetX, tile.GetY, tile.GetZ));
        var avatar = Assert.Single(AvatarEntries(sent));
        Assert.Equal((actor.VirtualId, 0.5, 0.8), (avatar.Id, avatar.FromZ, avatar.ToZ));
        AssertBoundTo(actor, tile, 0.8);
    }

    // A furni that cannot be walked on and lands on a user leaves him at its base height. A raise in the same wave moves him with no entry, a later one with an entry.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ANonWalkableFurniMovedOntoAUserAndRaisedLeavesTheUserAtItsBase(bool noAnimation)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: true, turn: false);
        await WalkTo(actor, 0, 3);

        var sent = await ClickABot();

        Assert.Equal((0, 3, 0.3), (tile.GetX, tile.GetY, tile.GetZ));
        Assert.Empty(AvatarEntries(sent));
        Assert.Equal(0.3, actor.Z, 3);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ANonWalkableFurniRaisedLaterTakesTheUserInsideItWithIt(bool noAnimation)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: true, turn: false, altitudeDelay: 1);
        await WalkTo(actor, 0, 3);

        var sent = await ClickABot();

        Assert.Equal((0, 3, 0.3), (tile.GetX, tile.GetY, tile.GetZ));
        var avatar = Assert.Single(AvatarEntries(sent));
        Assert.Equal((actor.VirtualId, 0, 0.3), (avatar.Id, avatar.FromZ, avatar.ToZ));
        Assert.Equal(0.3, actor.Z, 3);
    }

    // Wired raises the furni under the one a user stands on: he gets no entry, and his next update has the new height.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ARaiseOfTheFurniUnderTheFurniAUserStandsOnSendsNoEntry(bool noAnimation)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: true, turn: false, moveToUser: false);
        await UserOnTop(actor, tile, 0, stackMore: () => ExecutorFloor(StackedRug, 2, 2, z: tile.TotalHeight, height: 0.2));
        Assert.Equal(0.7, actor.Z, 3);

        var sent = await ClickABot();

        Assert.Equal(0.3, tile.GetZ, 3);
        Assert.Empty(AvatarEntries(sent));
        Assert.Equal(0.8, actor.Z, 3);
    }

    // A sitting user is lifted at his sitting height.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ASittingUserOnTheFurniGetsAnEntryAtTheSittingHeight(bool noAnimation)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: true, turn: false, moveToUser: false);
        await UserOnTop(actor, tile, 0);
        await new SitEvent(new RoomAvatarActionService(TimeProvider.System, null!, null!)).Parse(_client, ClientPacket(1));
        ExecutorTick();
        Assert.True(actor.IsSitting);

        var sent = await ClickABot();

        var avatar = Assert.Single(AvatarEntries(sent));
        Assert.Equal((actor.VirtualId, 1.05, 1.35), (avatar.Id, avatar.FromZ, avatar.ToZ));
        Assert.True(actor.IsSitting);
    }

    // A 2x1 furni turns on its first tile. The user on its second tile is left on the floor, the user on its first tile stays on it, and neither gets an entry.
    [Theory]
    [InlineData(false, 2, 0.0)]
    [InlineData(true, 2, 0.0)]
    [InlineData(false, 1, 0.5)]
    [InlineData(true, 1, 0.5)]
    public async Task ATurnThatChangesTheHeightUnderAUserSendsNoEntry(bool noAnimation, int userX, double expectedZ)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: false, turn: true, moveToUser: false);
        tile.Definition.Walkable = true;
        tile.Definition.Width = 2;
        Assert.True(WiredRoomOperations.MoveItem(_room, tile, 1, 2, height: 0, animate: false, announce: false));
        await WalkTo(actor, userX, 2);
        Assert.Equal(0.5, actor.Z, 3);

        var sent = await ClickABot();

        Assert.Equal((1, 2, 2), (tile.GetX, tile.GetY, tile.Rotation));
        Assert.Empty(AvatarEntries(sent));
        Assert.Equal(expectedZ, actor.Z, 3);
    }

    private sealed record AvatarEntry(int FromX, int FromY, int ToX, int ToY, double FromZ, double ToZ, int AnimationType, int DurationMs);

    private static List<AvatarEntry> AvatarEntryDetails(IEnumerable<(uint Header, byte[] Body)> sent)
    {
        var entries = new List<AvatarEntry>();

        foreach (var packet in sent.Where(packet => packet.Header == ServerPacketHeader.WiredMovementsComposer)) {
            var bytes = packet.Body;
            var position = 0;
            int Int()
            {
                var value = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(position));
                position += 4;

                return value;
            }
            string String()
            {
                var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(position));
                var value = System.Text.Encoding.UTF8.GetString(bytes, position + 2, length);
                position += 2 + length;

                return value;
            }

            for (var count = Int(); count > 0; count--) {
                var type = Int();

                if (type == 3) {
                    _ = (Int(), Int(), Int());

                    continue;
                }

                var (fromX, fromY, toX, toY) = (Int(), Int(), Int(), Int());
                var (fromZ, toZ) = (double.Parse(String(), System.Globalization.CultureInfo.InvariantCulture), double.Parse(String(), System.Globalization.CultureInfo.InvariantCulture));
                _ = Int();

                if (type == 1) {
                    _ = (Int(), Int());

                    if (bytes[position++] != 0) {
                        _ = Int();
                    }

                    if (bytes[position++] != 0) {
                        _ = Int();
                    }

                    continue;
                }

                var animationType = Int();
                var duration = Int();
                _ = (Int(), Int());
                entries.Add(new(fromX, fromY, toX, toY, fromZ, toZ, animationType, duration));

                if (bytes[position++] != 0) {
                    _ = Int();
                }
            }
        }

        return entries;
    }

    // The user's announced step goes onto the furni when Wired raises it: he gets one entry from his tile to the step target at the new height, which is not a slide.
    [Theory]
    [InlineData(false, 500)]
    [InlineData(true, 0)]
    public async Task AUserSteppingOntoAFurniThatWiredRaisesGetsAnEntryToTheStepTarget(bool noAnimation, int durationMs)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: true, turn: false, moveToUser: false);
        tile.Definition.Walkable = true;
        Assert.True(WiredRoomOperations.MoveItem(_room, tile, 2, 1, height: 0, animate: false, announce: false));
        Assert.True(WiredRoomOperations.MoveItem(_room, tile, 2, 2, height: 0, animate: false, announce: false));
        await WalkTo(actor, 2, 0);
        ExecutorTick();
        await new MoveAvatarEvent(new RoomAvatarActionService(TimeProvider.System, null!, null!)).Parse(_client, ClientPacket(2, 3));

        for (var tick = 0; tick < 40 && !(actor.SetStep && (actor.SetX, actor.SetY) == (2, 2)); tick++) {
            ExecutorTick();
        }

        Assert.True(actor.SetStep && (actor.SetX, actor.SetY) == (2, 2));
        Assert.Equal((2, 1), (actor.X, actor.Y));

        FireByChat(actor);

        Assert.Equal(0.3, tile.GetZ, 3);
        var entry = Assert.Single(AvatarEntryDetails(_client.Packets.ToArray()));
        Assert.Equal((2, 1, 2, 2, 0, 0.8, 0, durationMs), (entry.FromX, entry.FromY, entry.ToX, entry.ToY, entry.FromZ, entry.ToZ, entry.AnimationType, entry.DurationMs));
    }

    // The user's announced step goes off the furni: Wired raises it and sends him nothing.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AUserSteppingOffAFurniThatWiredRaisesGetsNoEntry(bool noAnimation)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: true, turn: false, moveToUser: false);
        tile.Definition.Walkable = true;
        Assert.True(WiredRoomOperations.MoveItem(_room, tile, 2, 1, height: 0, animate: false, announce: false));
        Assert.True(WiredRoomOperations.MoveItem(_room, tile, 2, 2, height: 0, animate: false, announce: false));
        await WalkTo(actor, 2, 2);
        ExecutorTick();
        await new MoveAvatarEvent(new RoomAvatarActionService(TimeProvider.System, null!, null!)).Parse(_client, ClientPacket(2, 3));

        for (var tick = 0; tick < 40 && !(actor.SetStep && (actor.SetX, actor.SetY) == (2, 3)); tick++) {
            ExecutorTick();
        }

        Assert.True(actor.SetStep && (actor.SetX, actor.SetY) == (2, 3));

        FireByChat(actor);

        Assert.Equal(0.3, tile.GetZ, 3);
        Assert.Empty(AvatarEntryDetails(_client.Packets.ToArray()));
    }

    // A seat or a bed: the user sits or lies at the height of the furni's posture on top of the furni's base, and the base rises with the furni.
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task AUserOnASeatOrABedGetsAnEntryAtThePostureHeight(bool noAnimation, bool bed)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: true, turn: false, moveToUser: false);
        tile.Definition.Walkable = true;
        tile.Definition.IsSeat = !bed;
        tile.Definition.InteractionType = bed ? InteractionType.Bed : InteractionType.None;
        Assert.True(WiredRoomOperations.MoveItem(_room, tile, 2, 1, height: 0, animate: false, announce: false));
        Assert.True(WiredRoomOperations.MoveItem(_room, tile, 2, 2, height: 0, animate: false, announce: false));
        await WalkTo(actor, 2, 2);
        ExecutorTick();
        ExecutorTick();
        Assert.True(actor.Statusses.ContainsKey(bed ? "lay" : "sit"));

        var sent = await ClickABot();

        var avatar = Assert.Single(AvatarEntries(sent));
        Assert.Equal((actor.VirtualId, 0.5, 0.8), (avatar.Id, avatar.FromZ, avatar.ToZ));
        Assert.Equal(0.3, actor.Z, 3);
    }

    // A furni that cannot be walked on, with a user inside it, is lowered in place: the user is lowered with it and gets an entry.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ANonWalkableFurniLoweredInPlaceOntoAUserLowersHimWithAnEntry(bool noAnimation)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: true, turn: false);
        await WalkTo(actor, 0, 3);
        FireByChat(actor);
        Assert.Equal((0, 3, 0.3), (tile.GetX, tile.GetY, tile.GetZ));
        Assert.Equal(0.3, actor.Z, 3);
        _client.Packets.Clear();
        await new SaveWiredEffectConfigEvent(_inPlaceService!).Parse(_client, ClientPacket((int)RuntimeBox, 7, 0, 0, 0, 0, 0, -10, 0, "", 1, (int)tile.Id, 0, 2, 100, 200, 2, 0, 200, 2, "-123", "0", 0));
        Assert.Equal("saved", SaveAnswer());

        FireByChat(actor);

        Assert.Equal((0, 3, 0), (tile.GetX, tile.GetY, tile.GetZ));
        var avatar = Assert.Single(AvatarEntries(_client.Packets.ToArray()));
        Assert.Equal((actor.VirtualId, 0.3, 0), (avatar.Id, avatar.FromZ, avatar.ToZ));
        Assert.Equal(0, actor.Z, 3);
    }

    // A sitting user is left behind by a furni that moves to another tile and rises in one wave: he gets no entry and sits on the floor.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ASittingUserLeftBehindByAFurniThatMovesAndRisesGetsNoEntry(bool noAnimation)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: true, turn: false, moveToUser: false, west: true);
        await UserOnTop(actor, tile, 0);
        await new SitEvent(new RoomAvatarActionService(TimeProvider.System, null!, null!)).Parse(_client, ClientPacket(1));
        ExecutorTick();
        Assert.True(actor.IsSitting);

        var sent = await ClickABot();

        Assert.Equal((1, 2, 0.3), (tile.GetX, tile.GetY, tile.GetZ));
        Assert.Empty(AvatarEntries(sent));
        Assert.True(actor.IsSitting);
        Assert.Equal(0, actor.Z, 3);
    }

    // A user who has asked to walk off the furni but whose step is not announced yet still stands on it and is lifted with it.
    [Theory]
    [InlineData(false, 500)]
    [InlineData(true, 0)]
    public async Task AUserWhoHasAskedToWalkOffButNotStartedIsLiftedLikeAStandingUser(bool noAnimation, int durationMs)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: true, turn: false, moveToUser: false);
        tile.Definition.Walkable = true;
        Assert.True(WiredRoomOperations.MoveItem(_room, tile, 2, 1, height: 0, animate: false, announce: false));
        Assert.True(WiredRoomOperations.MoveItem(_room, tile, 2, 2, height: 0, animate: false, announce: false));
        await WalkTo(actor, 2, 2);
        await new MoveAvatarEvent(new RoomAvatarActionService(TimeProvider.System, null!, null!)).Parse(_client, ClientPacket(2, 3));
        Assert.False(actor.SetStep);

        FireByChat(actor);

        var entry = Assert.Single(AvatarEntryDetails(_client.Packets.ToArray()));
        Assert.Equal((2, 2, 2, 2, 0.5, 0.8, 1, durationMs), (entry.FromX, entry.FromY, entry.ToX, entry.ToY, entry.FromZ, entry.ToZ, entry.AnimationType, entry.DurationMs));
    }

    // The announced step goes onto a raised furni when Wired lowers it: the entry runs to the step target at the lower height.
    [Theory]
    [InlineData(false, 500)]
    [InlineData(true, 0)]
    public async Task AUserSteppingOntoAFurniThatWiredLowersGetsAnEntryToTheLowerStepTarget(bool noAnimation, int durationMs)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: true, turn: false, moveToUser: false, altitudeValue: 0);
        tile.Definition.Walkable = true;
        Assert.True(WiredRoomOperations.MoveItem(_room, tile, 2, 1, height: 0.3, animate: false, announce: false));
        Assert.True(WiredRoomOperations.MoveItem(_room, tile, 2, 2, height: 0.3, animate: false, announce: false));
        await WalkTo(actor, 2, 0);
        ExecutorTick();
        await new MoveAvatarEvent(new RoomAvatarActionService(TimeProvider.System, null!, null!)).Parse(_client, ClientPacket(2, 3));

        for (var tick = 0; tick < 40 && !(actor.SetStep && (actor.SetX, actor.SetY) == (2, 2)); tick++) {
            ExecutorTick();
        }

        Assert.True(actor.SetStep && (actor.SetX, actor.SetY) == (2, 2));

        FireByChat(actor);

        Assert.Equal(0, tile.GetZ, 3);
        var entry = Assert.Single(AvatarEntryDetails(_client.Packets.ToArray()));
        Assert.Equal((2, 1, 2, 2, 0, 0.5, 0, durationMs), (entry.FromX, entry.FromY, entry.ToX, entry.ToY, entry.FromZ, entry.ToZ, entry.AnimationType, entry.DurationMs));
    }

    // A rider and his horse step onto a furni that Wired raises: the rider's entry comes first and runs one above the horse's, which starts one higher.
    [Theory]
    [InlineData(false, 500)]
    [InlineData(true, 0)]
    public async Task ARiderAndHisHorseSteppingOntoAFurniThatWiredRaisesBothGetAnEntry(bool noAnimation, int durationMs)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: true, turn: false, moveToUser: false);
        tile.Definition.Walkable = true;
        Assert.True(WiredRoomOperations.MoveItem(_room, tile, 2, 1, height: 0, animate: false, announce: false));
        Assert.True(WiredRoomOperations.MoveItem(_room, tile, 2, 2, height: 0, animate: false, announce: false));
        await WalkTo(actor, 2, 0);
        var horse = ExternalLifecycleHorse(2, 0);
        ExecutorTick();
        EstablishExternalMountedGroup(actor, horse);
        ExecutorTick();
        await new MoveAvatarEvent(new RoomAvatarActionService(TimeProvider.System, null!, null!)).Parse(_client, ClientPacket(2, 3));

        for (var tick = 0; tick < 40 && !(actor.SetStep && (actor.SetX, actor.SetY) == (2, 2)); tick++) {
            ExecutorTick();
        }

        Assert.True(actor.SetStep && (actor.SetX, actor.SetY) == (2, 2));

        FireByChat(actor);

        var entries = AvatarEntryDetails(_client.Packets.ToArray());
        Assert.Equal(2, entries.Count);
        Assert.Equal((2, 1, 2, 2, 2, 1.8, 0, durationMs), (entries[0].FromX, entries[0].FromY, entries[0].ToX, entries[0].ToY, entries[0].FromZ, entries[0].ToZ, entries[0].AnimationType, entries[0].DurationMs));
        Assert.Equal((2, 1, 2, 2, 0, 0.8, 0, durationMs), (entries[1].FromX, entries[1].FromY, entries[1].ToX, entries[1].ToY, entries[1].FromZ, entries[1].ToZ, entries[1].AnimationType, entries[1].DurationMs));
    }

    // The user says the trigger word through the real chat handler; the Wired cycle then runs the stack.
    private void FireByChat(RoomUser actor)
    {
        Talk(actor, "go");
        _room.GetWired().OnFastCycle();
        _room.GetWired().OnCycle();
    }

    private async Task StepTowardsTheFurni(RoomUser actor, int targetY)
    {
        ExecutorTick();
        await new MoveAvatarEvent(new RoomAvatarActionService(TimeProvider.System, null!, null!)).Parse(_client, ClientPacket(2, targetY));

        for (var tick = 0; tick < 40 && !(actor.SetStep && (actor.SetX, actor.SetY) == (2, 2)); tick++) {
            ExecutorTick();
        }

        Assert.True(actor.SetStep && (actor.SetX, actor.SetY) == (2, 2));
    }

    // A fast walker announces two steps at once; his entry goes to the last one, the furni tile, at the new height.
    [Theory]
    [InlineData(false, 500)]
    [InlineData(true, 0)]
    public async Task AFastWalkerSteppingOntoAFurniThatWiredRaisesGetsAnEntryToTheLastAnnouncedStep(bool noAnimation, int durationMs)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: true, turn: false, moveToUser: false);
        tile.Definition.Walkable = true;
        Assert.True(WiredRoomOperations.MoveItem(_room, tile, 2, 1, height: 0, animate: false, announce: false));
        Assert.True(WiredRoomOperations.MoveItem(_room, tile, 2, 2, height: 0, animate: false, announce: false));
        await WalkTo(actor, 2, 0);
        actor.FastWalking = true;
        await StepTowardsTheFurni(actor, 3);
        Assert.Equal((2, 0), (actor.X, actor.Y));

        FireByChat(actor);

        var entry = Assert.Single(AvatarEntryDetails(_client.Packets.ToArray()));
        Assert.Equal((2, 0, 2, 2, 0, 0.8, 0, durationMs), (entry.FromX, entry.FromY, entry.ToX, entry.ToY, entry.FromZ, entry.ToZ, entry.AnimationType, entry.DurationMs));
    }

    // The furni under a higher one is raised in place, but not above it: the rider's step target keeps its height, so neither the rider nor his horse gets an entry.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ARiderAndHisHorseGetNoEntryWhenTheRaisedFurniStaysUnderAHigherOne(bool noAnimation)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: true, turn: false, moveToUser: false);
        tile.Definition.Walkable = true;
        Assert.True(WiredRoomOperations.MoveItem(_room, tile, 2, 1, height: 0, animate: false, announce: false));
        Assert.True(WiredRoomOperations.MoveItem(_room, tile, 2, 2, height: 0, animate: false, announce: false));
        ExecutorFloor(StackedRug, 2, 2, z: tile.TotalHeight, height: 0.5);
        await WalkTo(actor, 2, 0);
        var horse = ExternalLifecycleHorse(2, 0);
        ExecutorTick();
        EstablishExternalMountedGroup(actor, horse);
        ExecutorTick();
        await StepTowardsTheFurni(actor, 3);

        FireByChat(actor);

        Assert.Equal(0.3, tile.GetZ, 3);
        Assert.Empty(AvatarEntryDetails(_client.Packets.ToArray()));
    }

    // Two users on one 2x1 furni that Wired lowers: each gets an entry; the order in the packet is not part of the rule.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EveryUserOnAMultiTileFurniIsLowered(bool noAnimation)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: true, turn: false, moveToUser: false, altitudeValue: 0);
        await UserOnTop(actor, tile, 0.3, width: 2);
        var other = ExecutorAdditionalBot(3, 3, 9);
        other.MoveTo(3, 2);

        for (var tick = 0; tick < 40 && ((other.X, other.Y) != (3, 2) || other.IsWalking || other.SetStep); tick++) {
            ExecutorTick();
        }

        Assert.Equal((3, 2), (other.X, other.Y));

        var sent = await ClickABot();

        var avatars = AvatarEntries(sent);
        Assert.Equal([actor.VirtualId, other.VirtualId], avatars.Select(entry => entry.Id).Order());
        Assert.All(avatars, entry => Assert.Equal((0.8, 0.5), (entry.FromZ, entry.ToZ)));
    }

    // Two stacks that fire on the same word and whose effects are due at the same time are two firings: the raise of one lifts the user the other stack moved the furni onto.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ARaiseFromAnotherFiringInTheSameTickLiftsTheUserTheFurniWasMovedOnto(bool noAnimation)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: true, turn: false, moveToUser: false);
        tile.Definition.Walkable = true;
        AddSpeechBox(950, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "go" }, 0, 3, 3);
        PlaceSavedBox(951, "wf_act_furni_to_user", 3, 3, 0.5);
        await new SaveWiredEffectConfigEvent(_inPlaceService!).Parse(_client, ClientPacket(951, 0, "", 1, (int)tile.Id, 0, 1, 100, 1, 0, 0, 0));
        Assert.Equal("saved", SaveAnswer());
        await WalkTo(actor, 0, 3);
        typeof(WiredStackEngine).GetField("_effectOrderRandom", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(EngineOf(_room.GetWired()), new UnshuffledRandom());
        var clock = new GridClock();
        var wired = _room.GetWired();
        typeof(WiredComponent).GetField("_clock", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(wired, clock);
        var engine = EngineOf(wired);
        engine.GetType().GetField("_now", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(engine, new Func<long>(() => clock.Ms));

        FireByChat(actor);

        Assert.Equal((0, 3, 0.3), (tile.GetX, tile.GetY, tile.GetZ));
        var avatar = Assert.Single(AvatarEntries(_client.Packets.ToArray()));
        Assert.Equal((0.5, 0.8), (avatar.FromZ, avatar.ToZ));
    }

    // A user sitting on the floor keeps sitting when a furni is placed under him by hand and when it is picked up again.
    [Fact]
    public async Task AFloorSittingUserKeepsSittingWhenAFurniIsPlacedUnderHimAndPickedUp()
    {
        var (actor, _) = await InPlaceRoom(noAnimation: false, altitude: false, turn: false, moveToUser: false);
        await WalkTo(actor, 0, 3);
        await new SitEvent(new RoomAvatarActionService(TimeProvider.System, null!, null!)).Parse(_client, ClientPacket(1));
        ExecutorTick();
        Assert.True(actor.IsSitting);

        var rug = Furni(StackedRug, InteractionType.None, WiredBoxType.None);
        rug.Definition.Walkable = true;
        rug.Definition.Height = 0.2;
        rug.Definition.Width = rug.Definition.Length = 1;
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(_client, rug, 0, 3, 0, true, false, false, height: 0));
        ExecutorTick();

        Assert.True(actor.IsSitting);
        Assert.Equal(0.2, actor.Z, 3);

        _room.GetRoomItemHandler().RemoveFurniture(null!, rug.Id);
        ExecutorTick();

        Assert.True(actor.IsSitting);
        Assert.Equal(0, actor.Z, 3);
    }

    private static object EngineOf(WiredComponent wired) => typeof(WiredComponent).GetField("_engine", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(wired)!;

    // Keeps stacks that fire together in the order of their triggers.
    private sealed class UnshuffledRandom : Random
    {
        public override int Next(int minValue, int maxValue) => minValue;
    }
}
