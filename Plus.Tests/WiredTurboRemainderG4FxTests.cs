using System.Collections.Immutable;
using Plus.Communication.Packets.Outgoing.WiredVariables;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Items.Wired.Variables.Fx;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// The six variable FX cards (wf_xtra_var_fx_health/progress/level/status/boss/number) of the g4 remainder batch, run end to end from the
/// native editor frame through validation, binding and the per viewer tracker to the packets a client receives. The editor frame is the one
/// observed on hhus (WIN63-202610131318): 23 ints, the category slot at [21] and the "Order in FX stack" at [22]. Runtime behaviour that
/// nothing official shows is read from turbo-cloud (facts only).
/// </summary>
public sealed class WiredTurboRemainderG4FxTests
{
    private static readonly (string Card, int Code, int Category)[] Cards =
    [
        ("wf_xtra_var_fx_health", 1200, 0), ("wf_xtra_var_fx_progress", 1201, 1), ("wf_xtra_var_fx_level", 1202, 2),
        ("wf_xtra_var_fx_status", 1203, 3), ("wf_xtra_var_fx_boss", 1204, 4), ("wf_xtra_var_fx_number", 1205, 5)
    ];

    public static TheoryData<string> AllCards => new(Cards.Select(card => card.Card));

    private static WiredBoxDescriptor Descriptor(string card)
    {
        Assert.True(WiredBoxRegistry.TryGet(card, out var descriptor));

        return descriptor;
    }

    private static WiredConfiguration? Compile(string card, Func<int[], int[]>? edit = null, string text = "", string[]? variables = null,
        int[]? replace = null)
    {
        var descriptor = Descriptor(card);
        var native = WiredNativeEditorProjection.DefaultNative(descriptor);
        var ints = replace ?? edit?.Invoke([.. native.OwnedIntParams]) ?? [.. native.OwnedIntParams];
        var proposed = native with
        {
            OwnedIntParams = [.. ints], Text = text, VariableIds = variables is null ? native.VariableIds : [.. variables]
        };

        return WiredNativeEditorProjection.TryCompile(50, descriptor, proposed, out var configuration) ? configuration : null;
    }

    /// <summary>The editor switching to furni source retargets both range overrides to the holder kind (sanitizeOverrideTarget).</summary>
    private static int[] Furni(int[] ints, int visibility = 2)
    {
        ints[0] = 0;
        ints[1] = visibility;
        ints[16] = ints[17] = 0;

        return ints;
    }

    private static WiredVariableFxBinding? Bind(string card, Func<int[], int[]>? edit = null, string text = "", string[]? variables = null,
        WiredVariableReference? shown = null, int[]? replace = null)
    {
        var configuration = Compile(card, edit, text, variables, replace);

        if (configuration is null) {
            return null;
        }

        var target = WiredVariableFxSettings.UsesUserSource(configuration) ? WiredVariableTarget.User : WiredVariableTarget.Furni;
        var reference = shown ?? new(target, target == WiredVariableTarget.User ? "custom:10" : "custom:20");

        return WiredVariableFxSettings.TryDecode(card, 50, configuration, reference, null, out var binding, out _) ? binding : null;
    }

    [Fact]
    public void TheSixCardsAreTheNativeAddonsOneTwoHundredToOneTwoHundredFive()
    {
        foreach (var (card, code, _) in Cards) {
            var descriptor = Descriptor(card);

            Assert.Equal(WiredBoxCategory.Addon, descriptor.Category);
            Assert.Equal(code, descriptor.EditorCode);
            Assert.Equal(code, WiredNativeEditorProjection.DefaultNative(descriptor).NativeCode);
        }
    }

    [Theory]
    [MemberData(nameof(AllCards))]
    public void EveryCardOpensOnTheObserved23IntFrameAndSavesAsIs(string card)
    {
        var native = WiredNativeEditorProjection.DefaultNative(Descriptor(card));

        Assert.Equal(23, native.OwnedIntParams.Length);
        Assert.Equal(3, native.VariableIds.Length);
        Assert.All(native.VariableIds, id => Assert.True(WiredVariableAbsent.Is(id)));
        // Source user, everyone, always, update mask 7, no hover, 3000 ms, first style; unset ranges 0..100; both overrides off but
        // targeting the holder; no audience value; no segments; empty category slot and stack order.
        Assert.Equal([1, 2, 0, 7, 0, 3000, 0], native.OwnedIntParams.Take(7));
        Assert.Equal([0, 0, 0, 100, 0, 0, 1, 1, 0, 0, 0], native.OwnedIntParams.Skip(10).Take(11).Select(value => value));
        Assert.Equal(0, native.OwnedIntParams[22]);

        var binding = Bind(card);

        Assert.NotNull(binding);
        Assert.Equal(Cards.Single(entry => entry.Card == card).Category, binding.Config.Category);
        Assert.True(binding.Config.UserFx);
        Assert.Equal(3000, binding.Config.DurationMs);
        Assert.Equal(7, binding.Config.UpdateMask);
        Assert.DoesNotContain("stack_order", binding.Config.Extra.Keys);
    }

    [Theory]
    [MemberData(nameof(AllCards))]
    public void TheOfficialEditorSendsZeroForEveryUnpickedVariableSlotAndTheCardStillSaves(string card)
    {
        // Observed on hhus: variableIds ["0","0","0"] in the initial frame and in every saved frame of the six cards.
        Assert.NotNull(Bind(card, variables: ["0", "0", "0"]));
        Assert.NotNull(Bind(card, variables: ["n", "n", "n"]));
        // With an audience restriction the third slot must be a real variable, and "0" is none.
        Assert.Null(Bind(card, ints => { ints[1] = 3; return ints; }, variables: ["0", "0", "0"]));
        Assert.NotNull(Bind(card, ints => { ints[1] = 3; return ints; }, variables: ["0", "0", "user:12"]));
    }

    [Theory]
    [MemberData(nameof(AllCards))]
    public void TheOrderInTheFxStackReachesTheClientOnlyWhenItIsNotZero(string card)
    {
        Assert.DoesNotContain("stack_order", Bind(card)!.Config.Extra.Keys);

        foreach (var order in new[] { 3, -7, int.MaxValue, int.MinValue }) {
            var binding = Bind(card, ints => { ints[22] = order; return ints; });

            Assert.NotNull(binding);
            Assert.Equal(order.ToString(System.Globalization.CultureInfo.InvariantCulture), binding.Config.Extra["stack_order"]);
        }

        var packet = new Packet();
        WiredVariableFxComposer.ComposeBatch(new(true, [Bind(card, ints => { ints[22] = 3; return ints; })!.Config], [], [], [])).Single().Compose(packet);
        Assert.Contains("stack_order", packet.Values.OfType<string>());
        Assert.Contains("3", packet.Values.OfType<string>());
    }

    [Theory]
    [MemberData(nameof(AllCards))]
    public void AFormSavedBeforeTheStackOrderKeepsItsSettingsAndTakesOrderZero(string card)
    {
        var category = Cards.Single(entry => entry.Card == card).Category;
        var current = WiredNativeEditorProjection.DefaultNative(Descriptor(card)).OwnedIntParams;
        // The 21 int form (22 for levelling and number displays) had no stack order and, for the other cards, no category slot.
        int[] legacy = category is 2 or 5 ? [.. current.Take(21), current[21]] : [.. current.Take(21)];
        legacy[5] = 4500;
        legacy[20] = 0;

        var configuration = Compile(card, replace: legacy);

        Assert.NotNull(configuration);
        Assert.Equal(23, configuration.IntParams.Length);
        Assert.Equal(4500, configuration.IntParams[5]);
        Assert.Equal(0, configuration.IntParams[22]);
        Assert.Equal(category == 2 ? current[21] : 0, configuration.IntParams[21]);
    }

    [Theory]
    [MemberData(nameof(AllCards))]
    public void OtherShapesAreRefused(string card)
    {
        var native = WiredNativeEditorProjection.DefaultNative(Descriptor(card)).OwnedIntParams;

        Assert.Null(Compile(card, replace: [.. native.Take(20)]));
        Assert.Null(Compile(card, replace: [.. native, 0]));
        Assert.Null(Compile(card, replace: []));
    }

    [Theory]
    [InlineData("wf_xtra_var_fx_health")]
    [InlineData("wf_xtra_var_fx_progress")]
    [InlineData("wf_xtra_var_fx_status")]
    [InlineData("wf_xtra_var_fx_boss")]
    public void OnlyLevellingAndNumberCardsUseTheCategorySlot(string card)
    {
        Assert.NotNull(Bind(card));
        Assert.Null(Bind(card, ints => { ints[21] = 1; return ints; }));
    }

    [Fact]
    public void ARangedCardRefusesAMaximumThatIsNotAboveTheMinimum()
    {
        Assert.NotNull(Bind("wf_xtra_var_fx_boss", ints => { ints[11] = 5; ints[13] = 6; return ints; }));
        Assert.Null(Bind("wf_xtra_var_fx_boss", ints => { ints[11] = 5; ints[13] = 5; return ints; }));
        Assert.Null(Bind("wf_xtra_var_fx_boss", ints => { ints[11] = 9; ints[13] = 5; return ints; }));
    }

    [Fact]
    public void ANumberDisplayAndALevelBadgeKeepTheirFixedRange()
    {
        var number = Bind("wf_xtra_var_fx_number");
        var level = Bind("wf_xtra_var_fx_level");

        Assert.Equal((0L, 100L), (number!.Config.Min, number.Config.Max));
        Assert.Equal((0L, 100L), (level!.Config.Min, level.Config.Max));
        // A number display accepts no other range; a levelling badge stores none (-1/-1, read as 0..100).
        Assert.Null(Bind("wf_xtra_var_fx_number", ints => { ints[13] = 50; return ints; }));
        Assert.NotNull(Bind("wf_xtra_var_fx_level", ints => { ints[10] = ints[11] = ints[12] = ints[13] = 7; return ints; }));
    }

    [Theory]
    [InlineData("battery", true)]
    [InlineData("wooden_logs", true)]
    [InlineData("ranch.corn", true)]
    [InlineData("cash", true)]
    [InlineData("", true)]
    [InlineData("misc_star", false)]
    [InlineData("misc_heart", false)]
    [InlineData("not_an_icon", false)]
    [InlineData("Battery", false)]
    public void ANumberDisplayCarriesOnlyAnIconTheClientHasAnAssetFor(string icon, bool accepted)
    {
        var binding = Bind("wf_xtra_var_fx_number", text: icon);

        Assert.Equal(accepted, binding is not null);

        if (accepted) {
            Assert.Equal(icon.Length > 0, binding!.Config.Extra.ContainsKey("icon"));
        }
    }

    [Theory]
    [InlineData(0, "left")]
    [InlineData(1, "right")]
    [InlineData(2, "double")]
    public void TheNumberIconAlignmentIsLeftRightOrDouble(int slot, string word)
    {
        var binding = Bind("wf_xtra_var_fx_number", ints => { ints[21] = slot; return ints; }, "gems");

        Assert.Equal(word, binding!.Config.Extra["icon_alignment"]);
        Assert.Equal("gems", binding.Config.Extra["icon"]);
        Assert.Null(Bind("wf_xtra_var_fx_number", ints => { ints[21] = 3; return ints; }, "gems"));
        Assert.Null(Bind("wf_xtra_var_fx_number", ints => { ints[21] = -1; return ints; }, "gems"));
    }

    [Theory]
    [InlineData("wf_xtra_var_fx_health")]
    [InlineData("wf_xtra_var_fx_progress")]
    [InlineData("wf_xtra_var_fx_level")]
    [InlineData("wf_xtra_var_fx_status")]
    [InlineData("wf_xtra_var_fx_boss")]
    public void OnlyANumberDisplayTakesAnIcon(string card) => Assert.Null(Bind(card, text: "gems"));

    [Theory]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    [InlineData(1, false)]
    [InlineData(20, false)]
    public void ALevelBadgeBarIsOneOfTheClientsThree(int bar, bool accepted) =>
        Assert.Equal(accepted, Bind("wf_xtra_var_fx_level", ints => { ints[21] = bar; return ints; }) is not null);

    [Fact]
    public void TheLevelBadgeBarReachesTheRendererAsItsSubRendererAndTakesSegments()
    {
        var binding = Bind("wf_xtra_var_fx_level", ints => { ints[21] = 4; ints[20] = 6; return ints; });

        Assert.Equal("4", binding!.Config.Extra["sub_renderer"]);
        Assert.Equal("6", binding.Config.Extra["segments"]);
        // Segments belong to block, arrow and thermometer bars only.
        Assert.Null(Bind("wf_xtra_var_fx_level", ints => { ints[21] = 3; ints[20] = 6; return ints; }));
        Assert.Null(Bind("wf_xtra_var_fx_progress", ints => { ints[20] = 6; return ints; }));
        Assert.Equal("6", Bind("wf_xtra_var_fx_progress", ints => { ints[6] = 1; ints[7] = 1; ints[9] = 2; ints[20] = 6; return ints; })!.Config.Extra["segments"]);
    }

    [Fact]
    public void ShowDurationShowModeAndMaskStayInTheEditorsBounds()
    {
        Assert.Null(Bind("wf_xtra_var_fx_health", ints => { ints[2] = 3; return ints; }));
        Assert.Null(Bind("wf_xtra_var_fx_health", ints => { ints[3] = 16; return ints; }));
        Assert.Null(Bind("wf_xtra_var_fx_health", ints => { ints[5] = 1499; return ints; }));
        Assert.Null(Bind("wf_xtra_var_fx_health", ints => { ints[5] = 20001; return ints; }));
        Assert.Null(Bind("wf_xtra_var_fx_health", ints => { ints[4] = 2; return ints; }));
        Assert.NotNull(Bind("wf_xtra_var_fx_health", ints => { ints[2] = 2; ints[3] = 15; ints[4] = 1; ints[5] = 1500; return ints; }));
    }

    [Fact]
    public void AFurniFxCannotBeRestrictedToTheHolderOrItsTeam()
    {
        // Furniture has no owner and no team: only "everyone", "has variable" and "has variable with value" exist for it.
        Assert.Null(Bind("wf_xtra_var_fx_health", ints => Furni(ints, 0)));
        Assert.Null(Bind("wf_xtra_var_fx_health", ints => Furni(ints, 1)));
        Assert.NotNull(Bind("wf_xtra_var_fx_health", ints => Furni(ints)));
        Assert.False(Bind("wf_xtra_var_fx_health", ints => Furni(ints))!.Config.UserFx);
        // A furni fx whose override still points at the user kind is not a form the editor writes.
        Assert.Null(Bind("wf_xtra_var_fx_health", ints => { ints[0] = 0; return ints; }));
        // The variable on the tile has to hang on what the fx is drawn over.
        Assert.Null(Bind("wf_xtra_var_fx_health", shown: new(WiredVariableTarget.Furni, "custom:20")));
        Assert.Null(Bind("wf_xtra_var_fx_health", ints => Furni(ints), shown: new(WiredVariableTarget.User, "custom:10")));
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    public void AnAudienceVariableIsPickedForExactlyTheTwoHasVariableAudiences(int visibility, bool needsVariable)
    {
        string[] none = [WiredVariableAbsent.Id, WiredVariableAbsent.Id, WiredVariableAbsent.Id];
        string[] audience = [WiredVariableAbsent.Id, WiredVariableAbsent.Id, "user:12"];

        Assert.Equal(!needsVariable, Bind("wf_xtra_var_fx_progress", ints => { ints[1] = visibility; return ints; }, variables: none) is not null);
        Assert.Equal(needsVariable, Bind("wf_xtra_var_fx_progress", ints => { ints[1] = visibility; return ints; }, variables: audience) is not null);
    }

    [Fact]
    public void ARangeOverrideNeedsItsVariableAndAFurniFxCannotOverrideFromAUserVariable()
    {
        string[] none = [WiredVariableAbsent.Id, WiredVariableAbsent.Id, WiredVariableAbsent.Id];
        string[] minimum = ["user:13", WiredVariableAbsent.Id, WiredVariableAbsent.Id];
        string[] roomWide = [WiredVariableAbsent.Id, "room:30", WiredVariableAbsent.Id];

        Assert.Null(Bind("wf_xtra_var_fx_progress", ints => { ints[14] = 1; return ints; }, variables: none));
        Assert.NotNull(Bind("wf_xtra_var_fx_progress", ints => { ints[14] = 1; return ints; }, variables: minimum));
        // A room variable can stand in for the maximum when the override targets the room (-10).
        Assert.NotNull(Bind("wf_xtra_var_fx_progress", ints => { ints[15] = 1; ints[17] = -10; return ints; }, variables: roomWide));
        Assert.Null(Bind("wf_xtra_var_fx_progress", ints => { ints[15] = 1; ints[17] = 1; return ints; }, variables: roomWide));
        Assert.Null(Bind("wf_xtra_var_fx_progress", ints => { ints[15] = 1; ints[17] = 7; return ints; }, variables: roomWide));
    }

    // The runtime: who sees which value, in what packets.

    private static readonly WiredVariableHolder Viewer = new(WiredVariableTarget.User, 901, 8);
    private static readonly WiredVariableHolder Holder = new(WiredVariableTarget.User, 900, 7);
    private static readonly WiredVariableHolder Third = new(WiredVariableTarget.User, 902, 9);
    private static readonly WiredVariableHolder Floor = new(WiredVariableTarget.Furni, 500, 500);

    private sealed class Directory : IWiredVariableDirectory
    {
        public WiredVariableDefinition? Find(uint id) => id switch
        {
            >= 10 and <= 15 => new(id, 1, 5, $"v{id}", WiredVariableTarget.User, WiredVariableAvailability.Persistent, true),
            20 => new(id, 1, 5, "crate", WiredVariableTarget.Furni, WiredVariableAvailability.RoomActive, true),
            30 => new(id, 1, 5, "limit", WiredVariableTarget.Global, WiredVariableAvailability.RoomActive, true, 40),
            _ => null
        };
        public uint? GetRoomOwner(uint roomId) => roomId == 1 ? 5u : null;
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class World
    {
        public WiredVariableModule Module { get; } = new(1, new Directory(), new MemoryWiredVariableStore(), new Clock(DateTimeOffset.FromUnixTimeMilliseconds(1000)));
        public WiredVariableFrame Frame { get; } = new(1, [Viewer, Holder, Third, Floor]);
        public WiredVariableFxTracker Tracker { get; }
        public World() => Tracker = new(Module);

        public void Give(uint id, WiredVariableHolder holder, long value) =>
            Assert.True(Module.Mutate(new(holder.Target, $"custom:{id}"), holder, WiredVariableMutation.Give, value, Frame));

        public WiredVariableFxBatch Update(WiredVariableFxBinding binding, WiredVariableHolder viewer, params WiredVariableHolder[] ready) =>
            Update([binding], viewer, _ => 0, ready);

        public WiredVariableFxBatch Update(IReadOnlyList<WiredVariableFxBinding> bindings, WiredVariableHolder viewer, Func<WiredVariableHolder, int> team,
            params WiredVariableHolder[] ready)
        {
            var batch = Tracker.Update(viewer, Frame, bindings, ready, team);
            Assert.True(Tracker.Acknowledge(viewer.StableId, batch));

            return batch;
        }
    }

    [Theory]
    [MemberData(nameof(AllCards))]
    public void EveryCardShowsTheValueOfTheVariableOverItsHolderAndNothingForAHolderWithoutOne(string card)
    {
        var world = new World();
        world.Give(10, Holder, 25);
        var binding = Bind(card)!;

        var batch = world.Update(binding, Viewer, Holder, Third);

        Assert.True(batch.InitializeAll);
        Assert.Equal(binding.Config.Id, Assert.Single(batch.Configs).Id);
        var status = Assert.Single(batch.Statuses);
        Assert.Equal((50, "user:10", true, 7), (status.Key.ConfigId, status.Key.VariableId, status.Key.UserEntity, status.Key.EntityId));
        Assert.Equal(25, status.Value);
        Assert.True(status.Initialize);
        Assert.Empty(batch.RemovedStatuses);

        // The value changes: only the status goes out, and it is a change, not a fresh holder.
        world.Give(10, Third, 1);
        Assert.True(world.Module.Mutate(new(WiredVariableTarget.User, "custom:10"), Holder, WiredVariableMutation.Set, 30, world.Frame));
        var changed = world.Update(binding, Viewer, Holder, Third);
        Assert.False(changed.InitializeAll);
        Assert.Empty(changed.Configs);
        Assert.Equal([30L, 1L], changed.Statuses.OrderBy(entry => entry.Key.EntityId == 7 ? 0 : 1).Select(entry => entry.Value));
        Assert.False(changed.Statuses.Single(entry => entry.Key.EntityId == 7).Initialize);
        Assert.True(changed.Statuses.Single(entry => entry.Key.EntityId == 9).Initialize);

        // Nothing moved: nothing to send.
        var quiet = world.Update(binding, Viewer, Holder, Third);
        Assert.Empty(quiet.Statuses);
        Assert.Empty(quiet.RemovedStatuses);
    }

    [Theory]
    [MemberData(nameof(AllCards))]
    public void TakingTheVariableOrLeavingTheRoomRemovesTheStatus(string card)
    {
        var world = new World();
        world.Give(10, Holder, 25);
        var binding = Bind(card)!;
        world.Update(binding, Viewer, Holder);

        Assert.True(world.Module.Mutate(new(WiredVariableTarget.User, "custom:10"), Holder, WiredVariableMutation.Remove, 0, world.Frame));
        var removed = world.Update(binding, Viewer, Holder);
        Assert.Equal(7, Assert.Single(removed.RemovedStatuses).EntityId);
        Assert.Empty(removed.Statuses);

        // A box taken away from the tile removes its config.
        var gone = world.Update([], Viewer, _ => 0, Holder);
        Assert.Equal(binding.Config.Id, Assert.Single(gone.RemovedConfigs));
    }

    [Fact]
    public void AHolderThatLeavesTheRoomIsDetachedAndComesBackAsANewHolder()
    {
        var world = new World();
        world.Give(10, Holder, 25);
        var binding = Bind("wf_xtra_var_fx_health")!;
        world.Update(binding, Viewer, Holder);

        world.Tracker.DetachHolder(Holder);
        var again = world.Update(binding, Viewer, Holder);

        Assert.True(Assert.Single(again.Statuses).Initialize);
    }

    [Fact]
    public void AFurniFxDrawsOverFurniAndNamesTheFurniVariable()
    {
        var world = new World();
        world.Give(20, Floor, 12);
        var binding = Bind("wf_xtra_var_fx_boss", ints => Furni(ints))!;

        var batch = world.Update(binding, Viewer, Floor, Holder);

        var status = Assert.Single(batch.Statuses);
        Assert.Equal(("furni:20", false, 500, 12L), (status.Key.VariableId, status.Key.UserEntity, status.Key.EntityId, status.Value));
        Assert.False(Assert.Single(batch.Configs).UserFx);
    }

    [Theory]
    [InlineData(0, true, false, false)]
    [InlineData(1, true, true, false)]
    [InlineData(2, true, true, true)]
    public void VisibilityOnlyTheHolderTheHoldersTeamOrEveryone(int visibility, bool seesOwn, bool seesTeammate, bool seesStranger)
    {
        var world = new World();
        world.Give(10, Holder, 1);
        world.Give(10, Third, 2);
        world.Give(10, Viewer, 3);
        var binding = Bind("wf_xtra_var_fx_progress", ints => { ints[1] = visibility; return ints; })!;
        // The viewer and the third user are on team 1, the holder on team 2.
        int Team(WiredVariableHolder holder) => holder.StableId == Holder.StableId ? 2 : 1;

        var batch = world.Update([binding], Viewer, Team, Holder, Third, Viewer);
        var seen = batch.Statuses.Select(entry => entry.Key.EntityId).ToHashSet();

        Assert.Equal(seesOwn, seen.Contains(8));
        Assert.Equal(seesTeammate, seen.Contains(9));
        Assert.Equal(seesStranger, seen.Contains(7));
    }

    [Fact]
    public void AViewerWithoutATeamSeesOnlyTheirOwnUnderGameTeamVisibility()
    {
        var world = new World();
        world.Give(10, Holder, 1);
        world.Give(10, Viewer, 3);
        var binding = Bind("wf_xtra_var_fx_progress", ints => { ints[1] = 1; return ints; })!;

        var seen = world.Update([binding], Viewer, _ => 0, Holder, Viewer).Statuses.Select(entry => entry.Key.EntityId);

        Assert.Equal([8], seen);
    }

    [Fact]
    public void TheHasVariableAudiencesShowOnlyToViewersThatHoldTheAudienceVariableAndLoseItWhenItGoes()
    {
        var world = new World();
        world.Give(10, Holder, 25);
        world.Give(12, Viewer, 4);
        var any = Bind("wf_xtra_var_fx_progress", ints => { ints[1] = 3; return ints; }, variables: [WiredVariableAbsent.Id, WiredVariableAbsent.Id, "user:12"])!;
        var exact = Bind("wf_xtra_var_fx_progress", ints => { ints[1] = 4; ints[19] = 5; return ints; }, variables: [WiredVariableAbsent.Id, WiredVariableAbsent.Id, "user:12"])!;

        Assert.Single(world.Update(any, Viewer, Holder).Statuses);
        // The audience variable holds 4, the box wants 5.
        Assert.Empty(world.Update(exact, Viewer, Holder).Statuses);
        Assert.True(world.Module.Mutate(new(WiredVariableTarget.User, "custom:12"), Viewer, WiredVariableMutation.Set, 5, world.Frame));
        Assert.Single(world.Update(exact, Viewer, Holder).Statuses);
        // A viewer without the variable sees nothing from either audience.
        Assert.Empty(world.Update(any, Third, Holder).Statuses);
    }

    [Fact]
    public void ARangeOverrideReplacesBothEndsAsAPairAndRepairsAnInvertedRange()
    {
        var world = new World();
        world.Give(10, Holder, 25);
        world.Give(11, Holder, 80);
        var binding = Bind("wf_xtra_var_fx_progress", ints => { ints[15] = 1; return ints; }, variables: [WiredVariableAbsent.Id, "user:11", WiredVariableAbsent.Id])!;

        var status = Assert.Single(world.Update(binding, Viewer, Holder).Statuses);

        // Only the maximum comes from the holder's variable; the end without a variable repeats the config's own (0).
        Assert.Equal((0L, 80L), (status.Min!.Value, status.Max!.Value));

        Assert.True(world.Module.Mutate(new(WiredVariableTarget.User, "custom:11"), Holder, WiredVariableMutation.Set, -5, world.Frame));
        var inverted = Assert.Single(world.Update(binding, Viewer, Holder).Statuses);
        Assert.Equal((0L, 1L), (inverted.Min!.Value, inverted.Max!.Value));
    }

    [Fact]
    public void ARoomVariableCanStandInForAnEndOfTheRange()
    {
        var world = new World();
        world.Give(10, Holder, 25);
        var binding = Bind("wf_xtra_var_fx_progress", ints => { ints[15] = 1; ints[17] = -10; return ints; }, variables: [WiredVariableAbsent.Id, "room:30", WiredVariableAbsent.Id])!;

        var status = Assert.Single(world.Update(binding, Viewer, Holder).Statuses);

        Assert.Equal((0L, 40L), (status.Min!.Value, status.Max!.Value));
    }

    [Fact]
    public void WithoutAnOverrideTheStatusCarriesNoRange()
    {
        var world = new World();
        world.Give(10, Holder, 25);

        var status = Assert.Single(world.Update(Bind("wf_xtra_var_fx_progress")!, Viewer, Holder).Statuses);

        Assert.Null(status.Min);
        Assert.Null(status.Max);
    }

    // The client matches the delegated colour by exact RGB against its own RED/GREEN/BLUE/YELLOW rows (VariableFxColorType.resolve);
    // 14625054, 3584586, 3898851 and 16767037.
    [Theory]
    [InlineData(1, "#df291e")]
    [InlineData(2, "#36b24a")]
    [InlineData(3, "#3b7de3")]
    [InlineData(4, "#ffd83d")]
    public void ATeamColourFxPaintsEachTeamInTheClientsOwnPalette(int team, string colour)
    {
        var world = new World();
        world.Give(10, Holder, 25);
        // Boss style 1 offers the team colour; a number display of the freeze style bakes the same four colours.
        var binding = Bind("wf_xtra_var_fx_boss", ints => { ints[6] = 1; ints[7] = 1002; return ints; })!;

        var status = Assert.Single(world.Update([binding], Viewer, holderTeam => holderTeam.StableId == Holder.StableId ? team : 0, Holder).Statuses);

        Assert.Equal(colour, status.Extra["delegated_color"]);
    }

    [Fact]
    public void AHolderOnNoTeamGetsNoTeamColourAndOtherColoursGetNone()
    {
        var world = new World();
        world.Give(10, Holder, 25);
        var teamColoured = Bind("wf_xtra_var_fx_boss", ints => { ints[6] = 1; ints[7] = 1002; return ints; })!;
        var green = Bind("wf_xtra_var_fx_progress")!;

        Assert.DoesNotContain("delegated_color", Assert.Single(world.Update([teamColoured], Viewer, _ => 0, Holder).Statuses).Extra.Keys);
        Assert.DoesNotContain("delegated_color", Assert.Single(world.Update([green], Third, _ => 1, Holder).Statuses).Extra.Keys);
    }

    [Fact]
    public void ALevelBadgeReadsLevelsFromTheLevelSystemOnItsTile()
    {
        var world = new World();
        world.Give(10, Holder, 150);
        world.Give(10, Third, 999);
        Assert.True(WiredVariableLevelSystem.TryParse("{\"mode\":1,\"stepSize\":100,\"maxLevel\":3}", out var levels));
        var binding = Bind("wf_xtra_var_fx_level")! with { Level = levels!.Level };

        var batch = world.Update(binding, Viewer, Holder, Third);

        var middle = batch.Statuses.Single(entry => entry.Key.EntityId == 7);
        Assert.Equal(("2", "3", "false"), (middle.Extra["current_level"], middle.Extra["max_level"], middle.Extra["is_maxed"]));
        Assert.Equal((100L, 200L), (middle.Min!.Value, middle.Max!.Value));
        // At the cap both ends are the last level's start, which is how the client draws the bar full.
        var top = batch.Statuses.Single(entry => entry.Key.EntityId == 9);
        Assert.Equal(("3", "3", "true"), (top.Extra["current_level"], top.Extra["max_level"], top.Extra["is_maxed"]));
        Assert.Equal((200L, 200L), (top.Min!.Value, top.Max!.Value));
    }

    [Fact]
    public void ALevelBadgeWithoutALevelSystemIsLevelOneOfOneOverTheConfigsRange()
    {
        var world = new World();
        world.Give(10, Holder, 150);

        var status = Assert.Single(world.Update(Bind("wf_xtra_var_fx_level")!, Viewer, Holder).Statuses);

        Assert.Equal(("1", "1", "false"), (status.Extra["current_level"], status.Extra["max_level"], status.Extra["is_maxed"]));
        Assert.Null(status.Min);
        Assert.Null(status.Max);
    }

    [Fact]
    public void ALevelBadgeInTheLevellingColourCarriesTheLevelExtras()
    {
        var world = new World();
        world.Give(10, Holder, 150);
        // Progress style 0 offers the team colour only; the levelling colour belongs to levelling styles, so a level badge style
        // with the levelling colour is the one that may carry the extras. A level badge reads them on every status.
        var badge = Bind("wf_xtra_var_fx_level", ints => { ints[7] = 1001; return ints; })!;

        var extra = Assert.Single(world.Update(badge, Viewer, Holder).Statuses).Extra;

        Assert.Contains("current_level", extra.Keys);
    }

    [Fact]
    public void ANumberDisplayCarriesNoRangeAndItsConfigIsZeroToOneHundred()
    {
        var world = new World();
        world.Give(10, Holder, -4);
        var binding = Bind("wf_xtra_var_fx_number", text: "fish")!;

        var batch = world.Update(binding, Viewer, Holder);

        var status = Assert.Single(batch.Statuses);
        Assert.Equal(-4, status.Value);
        Assert.Null(status.Min);
        Assert.Equal(("fish", "left"), (batch.Configs[0].Extra["icon"], batch.Configs[0].Extra["icon_alignment"]));
        Assert.Equal("freeze_style", batch.Configs[0].Extra["design"]);
    }

    [Fact]
    public void AViewerIsNeverSentMoreStatusesThanTheCap()
    {
        var module = new WiredVariableModule(1, new Directory(), new MemoryWiredVariableStore(), new Clock(DateTimeOffset.FromUnixTimeMilliseconds(1000)));
        var holders = Enumerable.Range(1, 10).Select(index => new WiredVariableHolder(WiredVariableTarget.User, 1000 + index, 100 + index)).ToArray();
        var frame = new WiredVariableFrame(1, [Viewer, .. holders]);

        foreach (var holder in holders) {
            Assert.True(module.Mutate(new(WiredVariableTarget.User, "custom:10"), holder, WiredVariableMutation.Give, holder.EntityId, frame));
        }

        var tracker = new WiredVariableFxTracker(module, maxStatuses: 4);
        var batch = tracker.Update(Viewer, frame, [Bind("wf_xtra_var_fx_progress")!], holders, _ => 0);

        Assert.Equal(4, batch.Statuses.Count);
    }

    [Fact]
    public void ACardKeepsItsStyleCatalogueAndRefusesWhatTheStyleDoesNotOffer()
    {
        // Health style 0 is the hearts bar: dynamic red-to-green only; boss style 0 is red with the skull icon, boss style 1 is free.
        // An option the style does not offer is not refused: the hotel stores the style's own default in its place.
        Assert.Equal(1000, Bind("wf_xtra_var_fx_health", ints => { ints[7] = 5; return ints; })!.Config.ColorId);
        Assert.Equal(5, Bind("wf_xtra_var_fx_boss", ints => { ints[7] = 1; return ints; })!.Config.ColorId);
        Assert.Equal(1, Bind("wf_xtra_var_fx_boss", ints => { ints[6] = 1; ints[7] = 1; return ints; })!.Config.ColorId);
        Assert.Null(Bind("wf_xtra_var_fx_status", ints => { ints[6] = 22; return ints; }));
        Assert.NotNull(Bind("wf_xtra_var_fx_status", ints => { ints[6] = 21; ints[7] = -1; return ints; }));
        // The battery runs red to green instead of a baked colour.
        Assert.Equal("battery", Bind("wf_xtra_var_fx_status", ints => { ints[6] = 16; ints[7] = 1000; return ints; })!.Config.Extra["icon"]);
        Assert.Equal(1000, Bind("wf_xtra_var_fx_status", ints => { ints[6] = 16; ints[7] = -1; return ints; })!.Config.ColorId);
    }

    [Fact]
    public void ABoxWithAnUnknownStyleIsRefusedAndNotRepaired()
    {
        Assert.Null(Bind("wf_xtra_var_fx_progress", ints => { ints[6] = 99; return ints; }));
        Assert.Null(Bind("wf_xtra_var_fx_number", ints => { ints[6] = 3; return ints; }));
    }

    [Fact]
    public void TheNormalisedStoredFormIsWhatTheRuntimeDecodes()
    {
        // The official editor opens with the style options unset (-1,-1,-1); the stored form has the style's own defaults.
        var configuration = Compile("wf_xtra_var_fx_health", ints => { ints[7] = ints[8] = ints[9] = -1; return ints; })!;

        Assert.Equal([1000, 2, 10], configuration.IntParams.Skip(7).Take(3));
    }

    [Fact]
    public void FxWithTheSameIdsInTwoRoomsKeepsEachRoomsBindingsApart()
    {
        // Config ids are item ids, so two cards never collide in a viewer's state; replacing a card with the same id replaces its config.
        var world = new World();
        world.Give(10, Holder, 25);
        var first = Bind("wf_xtra_var_fx_progress")!;
        var other = first with { Config = first.Config with { Id = 51, Category = 3 } };

        var batch = world.Update([first, other], Viewer, _ => 0, Holder);

        Assert.Equal([50, 51], batch.Configs.Select(config => config.Id).Order());
        Assert.Equal([50, 51], batch.Statuses.Select(status => status.Key.ConfigId).Order());
    }

    private sealed class Packet : IOutgoingPacket
    {
        public List<object> Values { get; } = [];
        public int MessageId { get; set; }
        public ReadOnlyMemory<byte> Buffer => default;
        public void WriteByte(byte value) => Values.Add(value);
        public void WriteShort(short value) => Values.Add(value);
        public void WriteInt(int value) => Values.Add(value);
        public void WriteInteger(int value) => Values.Add(value);
        public void WriteUInt(uint value) => Values.Add(value);
        public void WriteUInteger(uint value) => Values.Add(value);
        public void WriteBool(bool value) => Values.Add(value);
        public void WriteBoolean(bool value) => Values.Add(value);
        public void WriteString(string value) => Values.Add(value);
        public void WriteDouble(double value) => Values.Add(value);
    }
}
