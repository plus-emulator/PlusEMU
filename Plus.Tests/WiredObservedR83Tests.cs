using System.Runtime.CompilerServices;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// Official hhus WIN63-202610131318 batches r83-var-room, r83-placeholders and r83-logs: the opening frames and the behaviour the windows showed.
public sealed class WiredObservedR83Tests
{
    private const uint RoomVariable = 10;

    private static WiredConfiguration Compile(string name, int[] owned, string[] ids, int[]? furniSources = null, int[]? userSources = null, string text = "") =>
        WiredOctoberEditorParityTests.Compile(name, n =>
        {
            var edited = n with { OwnedIntParams = [.. owned], VariableIds = [.. ids], Text = text };

            if (furniSources != null) {
                edited = edited with { FurniSourceTypes = [.. furniSources] };
            }

            if (userSources != null) {
                edited = edited with { UserSourceTypes = [.. userSources] };
            }

            return edited;
        });

    // Official initial editor frames (receipts initial_editor of the r83 batches).
    [Theory]
    [InlineData("wf_cnd_var_val_match", "0,100,101,200,201;0,100,101,200,201", "0,200,201;0,200,201", "0,200", "0,200", true, "1,2,0,0,0,-10,0")]
    [InlineData("wf_xtra_text_output_variable", "0,100,200,201", "0,200,201", "100", "0", true, "0,1,0")]
    [InlineData("wf_xtra_var_text_connector", "", "", "", "", false, "")]
    [InlineData("wf_xtra_var_time_util", "", "", "", "", false, "0,1")]
    [InlineData("wf_xtra_var_lvlup_system", "", "", "", "", false, "0,1,100,50,50")]
    [InlineData("wf_var_room", "", "", "", "", false, "1")]
    [InlineData("wf_act_show_message", "", "0,200,201", "", "0", false, "0,202,-1")]
    [InlineData("wf_act_log", "", "", "", "", false, "0")]
    [InlineData("wf_act_neg_log", "", "", "", "", false, "0")]
    public void EditorFramesMatchTheOfficialInitialEditor(string name, string furniAllowed, string usersAllowed, string furniDefaults,
        string userDefaults, bool wall, string owned)
    {
        var metadata = WiredNativeEditorProjection.Metadata(name);
        string Groups(System.Collections.Immutable.ImmutableArray<System.Collections.Immutable.ImmutableArray<int>> groups) =>
            string.Join(';', groups.Select(group => string.Join(',', group)));

        Assert.Equal(furniAllowed, Groups(metadata.FurniAllowed));
        Assert.Equal(usersAllowed, Groups(metadata.UsersAllowed));
        Assert.Equal(furniDefaults, string.Join(',', metadata.FurniDefaults));
        Assert.Equal(userDefaults, string.Join(',', metadata.UserDefaults));
        Assert.Equal(wall, metadata.AllowWall);
        Assert.Equal(owned, string.Join(',', metadata.OwnedDefaults));
    }

    // Official: the opening [0, 1, 100, 50, 50] is accepted as it opens; saves carry only the selected mode's fields.
    [Fact]
    public void LevelSystemAcceptsItsOfficialOpeningFrameAndTheModeFields()
    {
        var opening = Compile("wf_xtra_var_lvlup_system", [0, 1, 100, 50, 50], []);
        var linear = Compile("wf_xtra_var_lvlup_system", [0, 1, 100, 50], []);

        Assert.Equal(opening.Text, linear.Text);
        Compile("wf_xtra_var_lvlup_system", [0, 2, 100, 50, 50], []);
        Assert.False(WiredOctoberEditorParityTests.TryCompile("wf_xtra_var_lvlup_system", n => n with { OwnedIntParams = [0, 2, 100, 50], VariableIds = [] }));
    }

    private static (WiredVariableModule Module, WiredVariableExecutors Executors, WiredVariableFrame Frame) Room()
    {
        var module = new WiredVariableModule(1, new Directory(), new MemoryWiredVariableStore(), TimeProvider.System);
        var frame = new WiredVariableFrame(1, []);

        return (module, new(module, TimeProvider.System), frame);
    }

    // Official r83-var-room: Variable Value (global == 7) failed before the say wrote the global and passed after Change Variable assigned 7.
    [Fact]
    public void RoomVariableFailsTheMatchUntilTheWriteAssignsIt()
    {
        var (module, executors, frame) = Room();
        var match = Compile("wf_cnd_var_val_match", [-10, 1, 0, 0, 7, -10, 0], ["room:10", "n"]);
        var set = Compile("wf_act_change_var_val", [-10, 0, 0, 0, 7, -10, 0], ["room:10", "n"]);
        var never = Compile("wf_cnd_var_val_match", [-10, 1, 0, 0, 999, -10, 0], ["room:10", "n"]);

        Assert.True(module.InitializeGlobal(RoomVariable));
        Assert.False(executors.Execute("wf_cnd_var_val_match", match, frame));
        Assert.True(executors.Execute("wf_act_change_var_val", set, frame));
        Assert.Equal(7, module.Read(new(WiredVariableTarget.Global, "custom:10"), new(WiredVariableTarget.Global, 0, 0), frame)!.Value);
        Assert.True(executors.Execute("wf_cnd_var_val_match", match, frame));
        Assert.False(executors.Execute("wf_cnd_var_val_match", never, frame));
    }

    // Official r83-placeholders: "$(r80vroom)" printed the room variable's value 7; with the text-mode placeholder over a "7=seven" connector it printed "seven".
    [Theory]
    [InlineData(0, "wiredprobe 7")]
    [InlineData(1, "wiredprobe seven")]
    public void PlaceholderPrintsTheRoomVariableAsValueOrConnectorText(int textMode, string expected)
    {
        var room = (Room)RuntimeHelpers.GetUninitializedObject(typeof(Room));
        room.Id = 1;
        var context = new WiredRuntimeContext(room, new(WiredEventKind.Enter), new(() => [], () => []), new Operations());
        var frame = WiredVariableRuntimeFrames.Create(context);
        context.VariableFrame = frame;
        var module = new WiredVariableModule(1, new Directory(), new MemoryWiredVariableStore(), TimeProvider.System);
        Assert.True(module.InitializeGlobal(RoomVariable));
        Assert.True(module.Mutate(new(WiredVariableTarget.Global, "custom:10"), new(WiredVariableTarget.Global, 0, 0), WiredVariableMutation.Set, 7, frame));
        Assert.True(WiredBoxRegistry.TryGet("wf_xtra_text_output_variable", out var descriptor));
        var box = new WiredVariableAddonBox(room, new Item { Id = 90 }, descriptor, module, _ => new Dictionary<int, string> { [7] = "seven", [8] = "eight" });
        WiredNativeTestSupport.Install(box, WiredNativeEditorProjection.DefaultNative(descriptor) with
        {
            OwnedIntParams = [0, -10, textMode], VariableIds = ["room:10"], FurniSourceTypes = [100], UserSourceTypes = [0], Text = "r80vroom"
        });

        Assert.True(box.Apply(context));
        Assert.Equal(expected, context.Policy.FormatText(context, "wiredprobe $(r80vroom)"));
    }

    // Official: write-to-logs entries are listed under source 1 (Wired) at the saved level; the client numbers the levels 0 INFO, 1 WARN, 2 ERROR, 3 DEBUG
    // (default texts wiredmenu.logs_overview.log_level.N) and its two sources 0 System, 1 Wired; "only the most recent 200 logs are stored per type".
    [Fact]
    public void RoomLogNumbersLevelsAndSourcesLikeTheClientAndBoundsEachTypeOnItsOwn()
    {
        Assert.Equal(["INFO", "WARNING", "ERROR", "DEBUG"], Enumerable.Range(0, 4).Select(WiredRoomLogEntry.LevelName));
        var log = new WiredRoomLog();
        var now = DateTimeOffset.UtcNow;

        for (var i = 0; i < 250; i++) {
            log.Append(WiredRoomLog.InfoLevel, 7, "wired " + i, now);
        }

        log.Append(WiredRoomLog.ErrorLevel, WiredLogSource.ExecutionCap, 0, "", "cap", now);
        log.Append(WiredRoomLog.ErrorLevel, WiredLogSource.RecursionTimeout, 0, "", "deep", now);

        Assert.Equal(200, log.Read(0, 100, source: WiredRoomLogEntry.WiredSource).Total);
        Assert.Equal("wired 249", log.Read(0, 1, source: WiredRoomLogEntry.WiredSource).Entries[0].Message);
        Assert.Equal(2, log.Read(0, 100, source: WiredRoomLogEntry.SystemSource).Total);
        Assert.All(log.Read(0, 100, source: WiredRoomLogEntry.SystemSource).Entries, entry => Assert.Equal((WiredRoomLog.ErrorLevel, WiredRoomLogEntry.SystemSource), (entry.Level, entry.WireSource)));
        Assert.Equal(202, log.Read(0, 100).Total);
        Assert.Equal(WiredLogSource.WiredLog, log.Summarize(1).Tallies[3].Source);
    }

    private sealed class Operations : IWiredRuntimeOperations
    {
        public bool CallStacks(WiredRuntimeContext context, IEnumerable<Item> targets, bool negative = false) => throw new NotSupportedException();
        public bool SendSignal(WiredRuntimeContext context, IEnumerable<Item> receivers, WiredSelection selection, bool negative = false) => throw new NotSupportedException();
        public void ResetTimers(IEnumerable<Item> targets) => throw new NotSupportedException();
    }

    private sealed class Directory : IWiredVariableDirectory
    {
        public WiredVariableDefinition? Find(uint itemId) => itemId == RoomVariable
            ? new(itemId, 1, 5, "r80vroom", WiredVariableTarget.Global, WiredVariableAvailability.RoomActive, true) : null;
        public uint? GetRoomOwner(uint roomId) => 5;
    }
}
