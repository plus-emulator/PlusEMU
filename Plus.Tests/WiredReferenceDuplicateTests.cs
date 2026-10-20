using System.Runtime.CompilerServices;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// A room holds one reference to a shared variable. Observed on AIR WIN63-202610201032-107964540: a second reference to the same
/// shared variable is refused with this key and no parameters.
/// </summary>
public sealed class WiredReferenceDuplicateTests
{
    private const uint Owner = 5;
    private const uint ExistingReference = 200;

    private static WiredVariableDefinition Reference(uint id, string name, string shared)
    {
        Assert.True(WiredVariableDefinitions.TryDecode("wf_var_reference", id, 2, Owner,
            new() { IntParams = [1], VariableIds = [shared], Text = name }, out var definition, out var error), error);

        return definition!;
    }

    private sealed class Directory : IWiredVariableDirectory
    {
        private readonly Dictionary<uint, WiredVariableDefinition> _definitions = new()
        {
            [100] = new(100, 1, Owner, "coins", WiredVariableTarget.Global, WiredVariableAvailability.Shared, true),
            [101] = new(101, 1, Owner, "gems", WiredVariableTarget.Global, WiredVariableAvailability.Shared, true),
            [ExistingReference] = Reference(ExistingReference, "wallet", "shared:1:room:100")
        };

        public WiredVariableDefinition? Find(uint id) => _definitions.GetValueOrDefault(id);
        public uint? GetRoomOwner(uint roomId) => Owner;
        public IReadOnlyList<WiredVariableDefinition> ListRoom(uint roomId) => [.. _definitions.Values.Where(x => x.RoomId == roomId)];
    }

    private static string? Save(uint itemId, string name, string shared)
    {
        var room = (Room)RuntimeHelpers.GetUninitializedObject(typeof(Room));
        room.Id = 2;
        room.OwnerId = (int)Owner;
        Assert.True(WiredBoxRegistry.TryGet("wf_var_reference", out var descriptor));
        var module = new WiredVariableModule(2, new Directory(), new MemoryWiredVariableStore(), new FixedTimeProvider(DateTimeOffset.FromUnixTimeMilliseconds(1)));
        var box = new WiredVariableDefinitionBox(room, new Item { Id = itemId, Definition = new() { InteractionName = "wf_var_reference" } }, descriptor, null, new(module));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with { OwnedIntParams = [1], Text = name, VariableIds = [shared] };
        Assert.True(WiredNativeEditorProjection.TryCompile(itemId, descriptor, native, out var proposed));

        return box.TryValidateConfiguration(proposed, out _, out var error) ? null : error;
    }

    [Fact]
    public void ASecondReferenceToTheSameSharedVariableIsRefused()
    {
        Assert.Equal("wiredfurni.error.variables.reference.already_exists", Save(201, "wallet2", "shared:1:room:100"));
    }

    [Fact]
    public void AReferenceToAnotherSharedVariableSaves()
    {
        Assert.Null(Save(201, "wallet2", "shared:1:room:101"));
    }

    [Fact]
    public void TheExistingReferenceCanBeSavedAgain()
    {
        Assert.Null(Save(ExistingReference, "wallet", "shared:1:room:100"));
    }
}
