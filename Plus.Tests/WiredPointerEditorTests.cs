using System.Runtime.CompilerServices;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// The Pointer Variable editor: the fresh frame, how a save is normalised and the refusal keys.
/// Observed on AIR WIN63-202610201032-107964540 (PointerVariable.as): every case below was saved on a pointer whose accessor was a
/// context variable with a value.
/// </summary>
public sealed class WiredPointerEditorTests
{
    private const uint Owner = 5;
    private const uint PointerId = 77;

    private static WiredVariableDefinition Plain(uint id, string name, WiredVariableTarget target, bool hasValue = true) =>
        new(id, 2, Owner, name, target, WiredVariableAvailability.RoomActive, hasValue);

    private static WiredVariableDefinition Pointer(uint id, string name, int target, string accessor)
    {
        Assert.True(WiredVariableDefinitions.TryDecode("wf_var_pointer", id, 2, Owner,
            new() { IntParams = [target], VariableIds = [accessor], Text = name }, out var definition, out var error), error);

        return definition!;
    }

    private sealed class Directory : IWiredVariableDirectory
    {
        public Dictionary<uint, WiredVariableDefinition> Definitions { get; } = new()
        {
            [40] = Plain(40, "pctx", WiredVariableTarget.Context),
            [41] = Plain(41, "pctx_nv", WiredVariableTarget.Context, hasValue: false),
            [42] = Plain(42, "puser", WiredVariableTarget.User),
            [43] = Plain(43, "pfurni", WiredVariableTarget.Furni),
            [44] = Plain(44, "pglob", WiredVariableTarget.Global),
            [45] = Pointer(45, "ptrctx", -20, "ctx:40"),
            [PointerId] = Pointer(PointerId, "ptr1", 1, "ctx:40")
        };

        public WiredVariableDefinition? Find(uint id) => Definitions.GetValueOrDefault(id);
        public uint? GetRoomOwner(uint roomId) => Owner;
        public IReadOnlyList<WiredVariableDefinition> ListRoom(uint roomId) => [.. Definitions.Values.Where(x => x.RoomId == roomId)];
    }

    private static (WiredVariableDefinitionBox Box, WiredBoxDescriptor Descriptor) Box()
    {
        var room = (Room)RuntimeHelpers.GetUninitializedObject(typeof(Room));
        room.Id = 2;
        room.OwnerId = (int)Owner;
        Assert.True(WiredBoxRegistry.TryGet("wf_var_pointer", out var descriptor));
        var module = new WiredVariableModule(2, new Directory(), new MemoryWiredVariableStore(), new FixedTimeProvider(DateTimeOffset.FromUnixTimeMilliseconds(1)));
        var item = new Item { Id = PointerId, Definition = new() { InteractionName = "wf_var_pointer" } };

        return (new(room, item, descriptor, null, new(module)), descriptor);
    }

    /// <summary>A raw save on the pointer box: the stored configuration, or the refusal key.</summary>
    private static (bool Saved, string Error, WiredConfiguration Stored) Save(int[] ints, string name, params string[] variables)
    {
        var (box, descriptor) = Box();
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with { OwnedIntParams = [.. ints], Text = name, VariableIds = [.. variables] };

        if (!WiredNativeEditorProjection.TryCompile(PointerId, descriptor, native, out var proposed)) {
            return (false, "compile", new());
        }

        var saved = box.TryValidateConfiguration(proposed, out var validated, out var error);

        return (saved, error, validated);
    }

    [Fact]
    public void AFreshPointerOpensOnTheUserTargetWithNoAccessor()
    {
        var (_, descriptor) = Box();
        var fresh = WiredNativeEditorProjection.DefaultNative(descriptor);

        Assert.Equal([1], fresh.OwnedIntParams.ToArray());
        Assert.Equal(["0"], fresh.VariableIds.ToArray());
        Assert.Equal("", fresh.Text);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(-10, -10)]
    [InlineData(-20, -20)]
    [InlineData(2, 0)]
    [InlineData(-5, 0)]
    [InlineData(100, 0)]
    public void ATargetThatIsNotOfferedIsStoredAsFurni(int sent, int stored)
    {
        var (saved, error, configuration) = Save([sent], "ptr1", "ctx:40");

        Assert.True(saved, error);
        Assert.Equal(stored, Assert.Single(configuration.IntParams));
    }

    [Theory]
    [InlineData(new int[0])]
    [InlineData(new[] { 1, 5 })]
    public void MissingOrExtraIntsAreStoredAsTheDefaultUserTarget(int[] ints)
    {
        var (saved, error, configuration) = Save(ints, "ptr1", "ctx:40");

        Assert.True(saved, error);
        Assert.Equal([1], configuration.IntParams.ToArray());
    }

    [Theory]
    [InlineData("ctx:41")]
    [InlineData("user:42")]
    [InlineData("furni:43")]
    [InlineData("room:44")]
    [InlineData("ctx:999")]
    public void TheAccessorMustBeAContextVariableWithAValue(string accessor)
    {
        var (saved, error, _) = Save([1], "ptr1", accessor);

        Assert.False(saved);
        Assert.Equal("wiredfurni.error.variables.missing_variable", error);
    }

    [Fact]
    public void NoAccessorAtAllIsRefused()
    {
        foreach (var variables in new[] { Array.Empty<string>(), ["0"] }) {
            var (saved, error, _) = Save([1], "ptr1", variables);

            Assert.False(saved);
            Assert.Equal("wiredfurni.error.variables.missing_variable", error);
        }
    }

    [Theory]
    [InlineData("ctx:45")]
    [InlineData("user:77")]
    public void APointerIsNeverAnAccessor(string accessor)
    {
        var (saved, error, _) = Save([1], "ptr1", accessor);

        Assert.False(saved);
        Assert.Equal("wiredfurni.error.variables.requires_execution_context", error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void AnEmptyNameOrOneOfFortyOneCharactersIsTooLong(string name)
    {
        var (saved, error, _) = Save([1], name, "ctx:40");

        Assert.False(saved);
        Assert.Equal("wiredfurni.error.variables.name_length", error);
    }

    [Fact]
    public void AFortyCharacterNameSaves()
    {
        var name = new string('a', 40);
        var (saved, error, configuration) = Save([1], name, "ctx:40");

        Assert.True(saved, error);
        Assert.Equal(name, configuration.Text);
    }

    [Fact]
    public void ADashInTheNameIsASyntaxError()
    {
        var (saved, error, _) = Save([1], "ptr-1", "ctx:40");

        Assert.False(saved);
        Assert.Equal("wiredfurni.error.variables.name_syntax", error);
    }

    [Fact]
    public void ASpaceInTheNameBecomesAnUnderscore()
    {
        var (saved, error, configuration) = Save([1], "ptr 1", "ctx:40");

        Assert.True(saved, error);
        Assert.Equal("ptr_1", configuration.Text);
    }

    [Theory]
    [InlineData("puser")]
    [InlineData("pctx")]
    public void TheNameOfAnotherVariableOfTheTargetOrOfTheAccessorIsTaken(string name)
    {
        var (saved, error, _) = Save([1], name, "ctx:40");

        Assert.False(saved);
        Assert.Equal("wiredfurni.error.variables.name_uniq", error);
    }

    [Theory]
    [InlineData("ptr1")]
    [InlineData("ptr_ok")]
    public void AConfiguredPointerKeepsOrChangesItsOwnName(string name)
    {
        var (saved, error, configuration) = Save([1], name, "ctx:40");

        Assert.True(saved, error);
        Assert.Equal(name, configuration.Text);
    }
}
