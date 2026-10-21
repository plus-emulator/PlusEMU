using Plus.Communication.Packets;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// The Pointer, Share and Import editors through the packet handler the client uses. The editor reopens on what the store keeps, so these
/// tests read the stored native record and the record the next editor open sends, not only the compiled runtime.
/// Observed on AIR WIN63-202610201032-107964540.
/// </summary>
public partial class PlacedFurniRoomTests
{
    /// <summary>A room whose variable module knows one context variable with a value, item 40, as the accessor of a pointer.</summary>
    private void RoomWithContextVariable()
    {
        var directory = new SharedAddonDirectory();
        directory.Rows[40] = new(40, RoomId, (uint)_room.OwnerId, "pctx", WiredVariableTarget.Context, WiredVariableAvailability.RoomActive, true);
        var variables = _room.GetWired().Variables;
        var module = new WiredVariableModule(RoomId, directory, new MemoryWiredVariableStore(), new FixedTimeProvider(DateTimeOffset.FromUnixTimeMilliseconds(1)));
        typeof(WiredRoomVariables).GetField("<Module>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(variables, module);
    }

    private (IWiredConfiguredItem Box, CanonicalStore Store) EditorBox(string name, InteractionType type)
    {
        if (name == "wf_var_pointer") {
            RoomWithContextVariable();
        }

        var store = new CanonicalStore(null);
        typeof(Plus.HabboHotel.Rooms.Instance.WiredComponent).GetField("_configurationStore", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(_room.GetWired(), store);
        var item = Furni(601, type, WiredBoxType.None);
        item.RoomId = RoomId;
        item.Definition.InteractionName = name;
        item.Definition.ItemName = name;
        _room.GetRoomItemHandler().LoadFurniture([item]);
        Assert.True(_room.GetWired().TryGet(item.Id, out var loaded));
        _client.Packets.Clear();

        return (Assert.IsAssignableFrom<IWiredConfiguredItem>(loaded), store);
    }

    private static object[] VariableSave(int[] ints, string text, string[] variables)
    {
        var values = new List<object> { 601, ints.Length };
        values.AddRange(ints.Cast<object>());
        values.Add(text);
        values.AddRange([0, 0, 0, variables.Length]);
        values.AddRange(variables);
        values.Add(0);

        return [.. values];
    }

    /// <summary>
    /// A raw save through the packet handler the client uses, then the record the editor opens on next. The stored record is what the
    /// store receives, so the reopened record must equal it.
    /// </summary>
    private async Task<WiredNativeEditorConfiguration> SaveAndReopen(IWiredConfiguredItem box, CanonicalStore store, int[] ints, string text)
    {
        var logger = new ErrorCapture();
        await new SaveWiredAddonConfigEvent(new WiredConfigurationService(store, null!, null!, logger)).Parse(_client, ClientPacket(VariableSave(ints, text, [])));
        Assert.True(!_client.Packets.Any(packet => packet.Header == Plus.Communication.Packets.Outgoing.ServerPacketHeader.WiredValidationErrorComposer), logger.Errors);
        var kept = Assert.Single(store.Saves).Origin!.Native;
        var reopened = Assert.IsType<WiredNativeEditorConfiguration>(WiredEditorSnapshot.Capture(box).Native);
        Assert.Equal(kept.OwnedIntParams.ToArray(), reopened.OwnedIntParams.ToArray());
        Assert.Equal(kept.Text, reopened.Text);

        return reopened;
    }

    /// <summary>
    /// A pointer save the way the handler runs it: the packet reader, the compile, the box validation and the editor reopen. The step that
    /// writes the definition to the database is left out because a unit test has no database; it stores this same native record.
    /// </summary>
    private WiredNativeEditorConfiguration SavePointerAndReopen(IWiredConfiguredItem box, int[] ints, string text)
    {
        var packet = ClientPacket(VariableSave(ints, text, ["ctx:40"]));
        packet.ReadInt();
        Assert.True(WiredLegacyProtocol.TryReadNative(packet, WiredBoxCategory.Variable, out var native));
        Assert.True(WiredNativeEditorProjection.TryCompile(601, box.Descriptor, native with { NativeCode = box.Descriptor.EditorCode }, out var runtime));
        Assert.True(box.TryValidateConfiguration(runtime, out var validated, out var error), error);
        box.ApplyConfiguration(validated);

        return Assert.IsType<WiredNativeEditorConfiguration>(WiredEditorSnapshot.Capture(box).Native);
    }

    [Fact]
    public void APointerSaveWithoutIntsReopensOnTheUserTarget()
    {
        var (box, _) = EditorBox("wf_var_pointer", InteractionType.WiredVariable);
        var reopened = SavePointerAndReopen(box, [], "ptr1");

        Assert.Equal([1], reopened.OwnedIntParams.ToArray());
    }

    [Theory]
    [InlineData(2)]
    [InlineData(-5)]
    [InlineData(100)]
    public void APointerTargetThatIsNotOfferedReopensAsFurni(int sent)
    {
        var (box, _) = EditorBox("wf_var_pointer", InteractionType.WiredVariable);
        var reopened = SavePointerAndReopen(box, [sent], "ptr1");

        Assert.Equal([0], reopened.OwnedIntParams.ToArray());
    }

    [Fact]
    public void ExtraPointerIntsAreDroppedOnReopen()
    {
        var (box, _) = EditorBox("wf_var_pointer", InteractionType.WiredVariable);
        var reopened = SavePointerAndReopen(box, [1, 5], "ptr1");

        Assert.Equal([1], reopened.OwnedIntParams.ToArray());
    }

    [Fact]
    public void ASpaceInAPointerNameReopensAsAnUnderscore()
    {
        var (box, _) = EditorBox("wf_var_pointer", InteractionType.WiredVariable);
        var reopened = SavePointerAndReopen(box, [1], "ptr 1");

        Assert.Equal("ptr_1", reopened.Text);
    }

    [Theory]
    [InlineData("wf_xtra_var_share")]
    [InlineData("wf_xtra_var_import")]
    public async Task ShareAndImportFlagsReopenNormalisedAndWithoutTheString(string name)
    {
        var (box, store) = EditorBox(name, InteractionType.WiredAddon);
        var reopened = await SaveAndReopen(box, store, [2, -1, 99], "hello");

        Assert.Equal([1, 1, 1], reopened.OwnedIntParams.ToArray());
        Assert.Equal("", reopened.Text);
    }

    [Theory]
    [InlineData("wf_xtra_var_share")]
    [InlineData("wf_xtra_var_import")]
    public async Task ShareAndImportMissingFlagsReopenOnAndExtraFlagsAreDropped(string name)
    {
        var (box, store) = EditorBox(name, InteractionType.WiredAddon);
        var reopened = await SaveAndReopen(box, store, [0], "");

        Assert.Equal([0, 1, 1], reopened.OwnedIntParams.ToArray());
    }

    private sealed class ErrorCapture : Microsoft.Extensions.Logging.ILogger<WiredConfigurationService>
    {
        public string Errors { get; private set; } = "";
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Errors += exception?.ToString() ?? formatter(state, exception);
    }
}
