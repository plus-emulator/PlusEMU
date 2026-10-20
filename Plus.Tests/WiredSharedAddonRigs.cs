using System.Collections.Concurrent;
using System.Buffers.Binary;
using System.Reflection;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms;
using Xunit;
using static Plus.Tests.SharedAddonIds;

namespace Plus.Tests;

/// <summary>The ids of the Share and Import fixture. One room holds the shared variable (item 10), another its reference (item 200).</summary>
internal static class SharedAddonIds
{
    public const uint Owner = 1;
    public const uint SharedVariable = 10, Connector = 20, Share = 21, Level = 22, Fx = 23, OtherVariable = 11, OtherConnector = 24, OtherShare = 25;
    public const uint Reference = 200, Import = 201, OtherReference = 202, OtherImport = 203, ThirdReference = 204;
    public const string TextConnectors = "1=one\n2=two";
    public const string LevelSystem = "{\"mode\":1,\"stepSize\":100,\"maxLevel\":3,\"subvariables\":[0,1,6]}";
}

internal sealed class SharedAddonDirectory : IWiredVariableDirectory
{
    public Dictionary<uint, WiredVariableDefinition> Rows { get; } = [];
    public WiredVariableDefinition? Find(uint id) => Rows.GetValueOrDefault(id);
    public uint? GetRoomOwner(uint roomId) => Owner;
    public IReadOnlyList<WiredVariableDefinition> ListRoom(uint roomId) => [.. Rows.Values.Where(x => x.RoomId == roomId)];
    public IReadOnlyList<WiredVariableDefinition> ListReferences(uint sourceRoomId, uint sourceItemId) =>
        [.. Rows.Values.Where(x => x.Link is { } link && link.SourceRoomId == sourceRoomId && link.Source.Token == $"custom:{sourceItemId}")];
}

internal sealed class SharedAddonRig
{
    public WiredChestProtocolTests.World World { get; } = new();
    public SharedAddonDirectory Directory { get; }
    public WiredRoomVariables Variables => World.Room.GetWired().Variables;
    public ConcurrentDictionary<uint, Item> Floor => (ConcurrentDictionary<uint, Item>)typeof(RoomItemHandling)
        .GetField("_floorItems", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(World.Room.GetRoomItemHandler())!;
    public uint RoomId { get; }

    /// <summary>The room whose shared variables this room's references read.</summary>
    public uint SourceRoomId { get; set; } = 42;
    private readonly WiredSharedVariableBroadcast.Endpoint _endpoint;
    public SharedAddonRig(uint roomId, SharedAddonDirectory directory, IWiredSharedAddonStore store)
    {
        RoomId = roomId;
        Directory = directory;
        World.Room.Id = roomId;
        World.Habbo.Access = EditorTestSupport.Access([]);
        World.Habbo.Username = "owner";
        World.Room.Type = "private";
        typeof(Plus.HabboHotel.Rooms.Room).GetProperty("OwnerName")!.SetValue(World.Room, "owner");
        var module = new WiredVariableModule(roomId, directory, new MemoryWiredVariableStore(), World.Clock, new RoomWiredBuiltinVariables(World.Room));
        typeof(WiredRoomVariables).GetField("<Module>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Variables, module);
        Variables.SharedAddons = store;
        // The room registers itself under the id it was built with; this one has been renamed since.
        _endpoint = new(module, _ => 0, Variables.InvalidateFx);
        WiredSharedVariableBroadcast.Register(_endpoint);
    }

    private Item Place(uint id, string name, int x)
    {
        var item = new Item { Id = id, RoomId = RoomId, OwnerId = Owner, GetX = x, Definition = new() { Id = id, InteractionName = name } };
        Floor[id] = item;

        return item;
    }

    public WiredVariableDefinitionBox AddDefinition(uint id, string name, WiredNativeEditorConfiguration? native = null, int x = 0)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var box = new WiredVariableDefinitionBox(World.Room, Place(id, name, x), descriptor);
        WiredNativeTestSupport.Install(box, native ?? WiredNativeEditorProjection.DefaultNative(descriptor));
        Assert.True(WiredVariableDefinitions.TryDecode(name, id, RoomId, Owner, box.Configuration, out var definition, out _));
        Directory.Rows[id] = definition!;
        Variables.ConfigurationLoaded(box);

        return box;
    }

    /// <summary>A shared user variable "shv" (has a value, availability 11).</summary>
    public void AddShared(uint id, string name, int x = 0)
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_var_user", out var descriptor));
        AddDefinition(id, "wf_var_user", WiredNativeEditorProjection.DefaultNative(descriptor) with { OwnedIntParams = [11, 1], Text = name }, x);
    }

    public void AddReference(uint id, string name, uint shared, int x = 0)
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_var_reference", out var descriptor));
        AddDefinition(id, "wf_var_reference", WiredNativeEditorProjection.DefaultNative(descriptor) with
        {
            OwnedIntParams = [0],
            Text = name,
            VariableIds = [$"shared:{SourceRoomId}:user:{shared}"]
        }, x);
    }

    public WiredVariableMetadataBox AddMetadata(uint id, string name, int[]? ints = null, string text = "", int x = 0)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var box = new WiredVariableMetadataBox(World.Room, Place(id, name, x), descriptor);
        Change(box, ints, text);
        Variables.ConfigurationLoaded(box);

        return box;
    }

    public static void Change(WiredVariableMetadataBox box, int[]? ints = null, string text = "")
    {
        var native = WiredNativeEditorProjection.DefaultNative(box.Descriptor);

        if (box.Descriptor.CanonicalName == "wf_xtra_var_lvlup_system" || box.Descriptor.CanonicalName == "wf_xtra_var_text_connector") {
            WiredNativeTestSupport.InstallLegacyVariableDraft(box, new() { Text = text });

            return;
        }

        WiredNativeTestSupport.Install(box, ints is null ? native : native with { OwnedIntParams = [.. ints] });
    }

    public WiredNativeVariable[] Own()
    {
        Assert.True(Variables.TryCaptureNativeCatalog(out var catalog));

        return [.. catalog!.Variables.Where(variable => !WiredHabboVariableIds.TryResolve(variable.Id, out _, out _))];
    }

    public WiredNativeVariable? Row(string name) => Own().SingleOrDefault(variable => variable.Name == name);
    public string[] Connector(string name) => [.. Row(name)?.Connector?.Select(pair => $"{pair.Key}={pair.Value}") ?? []];
    public string[] Parts(string parent) => [.. Own().Select(variable => variable.Name).Where(name => name.StartsWith(parent + ".", StringComparison.Ordinal)).Order()];
}

/// <summary>Room 42: the shared variable, its connector and its Share add-on in one stack. Room 43: a reference with an Import add-on.</summary>
internal sealed class SharedAddonScene
{
    public SharedAddonDirectory Directory { get; } = new();
    public MemoryWiredSharedAddonStore Store { get; } = new();
    public SharedAddonRig A { get; }
    public SharedAddonRig B { get; }
    public WiredVariableMetadataBox ConnectorBox { get; }
    public WiredVariableMetadataBox ShareBox { get; }
    public WiredVariableMetadataBox ImportBox { get; }
    private static uint _nextRoom = 1000;

    public SharedAddonScene(bool reference = true, int[]? share = null, int[]? import = null)
    {
        // Rooms announce themselves in a process-wide list by their id, so every scene takes ids of its own.
        var first = Interlocked.Add(ref _nextRoom, 2);
        A = new(first, Directory, Store);
        B = new(first + 1, Directory, Store) { SourceRoomId = first };
        A.AddShared(SharedVariable, "shv");
        ConnectorBox = A.AddMetadata(Connector, "wf_xtra_var_text_connector", text: TextConnectors);
        ShareBox = A.AddMetadata(Share, "wf_xtra_var_share", share);

        if (reference) {
            B.AddReference(Reference, "shref", SharedVariable);
        }

        ImportBox = B.AddMetadata(Import, "wf_xtra_var_import", import);
    }

    public void Synchronize(uint share = Share) => Assert.True(A.Variables.Synchronize(A.Floor[share]));
}

