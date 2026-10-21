using System.Text;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// A Wired box saved through the real packet path: a raw save body is parsed by the same incoming event the client reaches, handed to
/// the real save service, and the stored form is read back the way the editor shows it when it is opened again.
/// </summary>
internal sealed class WiredRawSave : IDisposable
{
    internal sealed record Result(string Answer, string? Key, int[] Ints, string Text, int[] Furni, int[] Users, int? Quantifier, int? Delay,
        string[]? Variables = null, IReadOnlyDictionary<string, string>? ErrorParameters = null)
    {
        public bool Accepted => Answer == "ok";

        public override string ToString() => Accepted
            ? $"ok ints=[{string.Join(',', Ints)}] text='{Text}' furni=[{string.Join(',', Furni)}] users=[{string.Join(',', Users)}] q={Quantifier} delay={Delay}"
            : $"{Answer} key={Key}";
    }

    private readonly G1World _world = new();
    private readonly G1Player _player;
    private readonly IWiredConfiguredItem _box;
    private readonly WiredBoxDescriptor _descriptor;
    private readonly IPacketEvent _handler;
    private readonly Item _item;

    public WiredRawSave(string name)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out _descriptor!));
        _player = _world.AddPlayer(1);
        _world.Room.OwnerName = "user1";
        _world.Room.Type = "private";
        _item = _world.AddItem(name, InteractionTypes.GetTypeFromString(name));
        _box = _world.Wired.CreateConfiguredBox(_item) ?? throw new InvalidOperationException(name);
        Assert.True(_world.Wired.AddBox((IWiredItem)_box));
        var service = new WiredConfigurationService(TestWiredConfigurationStore.Instance, null!, new FakeWordFilter(), TestLogging.For<WiredConfigurationService>());
        _handler = _descriptor.Category switch
        {
            WiredBoxCategory.Action => new SaveWiredEffectConfigEvent(service),
            WiredBoxCategory.Condition => new SaveWiredConditionConfigEvent(service),
            WiredBoxCategory.Trigger => new SaveWiredTriggerConfigEvent(service),
            WiredBoxCategory.Selector => new SaveWiredSelectorConfigEvent(service),
            WiredBoxCategory.Addon => new SaveWiredAddonConfigEvent(service),
            _ => new SaveWiredVariableConfigEvent(service)
        };
    }

    public void Dispose() => _world.Dispose();

    /// <summary>A floor furni the box can pick, of the kind its editor accepts.</summary>
    public uint Furni(string interaction = "test") => _world.AddItem(interaction, InteractionType.None).Id;

    /// <summary>A floor furni of the given kind, for the boxes that only accept some kinds as picks.</summary>
    public uint Furni(string interaction, InteractionType type) => _world.AddItem(interaction, type).Id;

    /// <summary>A Furni Chest the chest boxes accept as a pick.</summary>
    public uint FurniChest() => _world.AddChest(Plus.HabboHotel.Items.Wired.Chests.WiredChestKind.Furni).Id;

    /// <summary>
    /// Sends one raw save. Anything left out takes the box's fresh editor value, so a call names only what it probes.
    /// </summary>
    public Result Save(int[]? ints = null, string text = "", int[]? furni = null, int[]? users = null, int quantifier = 0, int delay = 0,
        string[]? variables = null, uint[]? picks = null, uint[]? secondary = null, bool filter = false, bool inverse = false)
    {
        var fresh = WiredNativeEditorProjection.DefaultNative(_descriptor);
        ints ??= [.. fresh.OwnedIntParams];
        furni ??= [.. fresh.FurniSourceTypes];
        users ??= [.. fresh.UserSourceTypes];
        variables ??= [.. fresh.VariableIds];
        picks ??= [];
        secondary ??= [];
        List<object> body = [(int)_item.Id, ints.Length, .. ints.Cast<object>(), text, picks.Length, .. picks.Select(id => (object)(int)id)];

        switch (_descriptor.Category) {
            case WiredBoxCategory.Action:
                body.Add(delay);
                break;
            case WiredBoxCategory.Condition:
                body.Add(quantifier);
                break;
            case WiredBoxCategory.Selector:
                body.Add(filter);
                body.Add(inverse);
                break;
        }

        body.AddRange([furni.Length, .. furni.Cast<object>(), users.Length, .. users.Cast<object>(), variables.Length, .. variables.Cast<object>(), secondary.Length,
            .. secondary.Select(id => (object)(int)id)]);
        _player.Packets.Clear();
        _handler.Parse(_player.Client, EditorTestSupport.Incoming([.. body])).GetAwaiter().GetResult();
        var answer = "none";
        string? key = null;
        Dictionary<string, string>? parameters = null;

        foreach (var packet in _player.Packets) {
            if (packet.Header == ServerPacketHeader.HideWiredConfigComposer) {
                answer = "ok";
            }
            else if (packet.Header == ServerPacketHeader.WiredValidationErrorComposer) {
                answer = "error";
                (key, parameters) = ReadError(packet.Payload);
            }
        }

        return Stored(answer, key) with { ErrorParameters = parameters };
    }

    // A key, then a count of (placeholder, value) string pairs, as WiredValidationErrorComposer writes them.
    private static (string Key, Dictionary<string, string> Parameters) ReadError(byte[] payload)
    {
        var offset = 0;

        string ReadString()
        {
            var length = (payload[offset] << 8) | payload[offset + 1];
            var text = Encoding.UTF8.GetString(payload, offset + 2, length);
            offset += 2 + length;

            return text;
        }

        var key = ReadString();
        var count = (payload[offset] << 24) | (payload[offset + 1] << 16) | (payload[offset + 2] << 8) | payload[offset + 3];
        offset += 4;
        var parameters = new Dictionary<string, string>();

        for (var index = 0; index < count; index++) {
            parameters[ReadString()] = ReadString();
        }

        return (key, parameters);
    }

    /// <summary>The furni the box keeps picked now, as the open-editor frame lists them.</summary>
    public uint[] Picks() => [.. _box.Configuration.SelectedItems];

    /// <summary>What the editor shows now: the stored form, as the open-editor frame carries it.</summary>
    public Result Stored(string answer = "ok", string? key = null)
    {
        var native = WiredNativeEditorProjection.HotelFrame(_descriptor, _box.Configuration.Origin!.Native);
        var metadata = WiredNativeEditorProjection.Metadata(_descriptor.CanonicalName);

        return new(answer, key, [.. native.OwnedIntParams], native.Text, [.. Padded(native.FurniSourceTypes, metadata.FurniDefaults)],
            [.. Padded(native.UserSourceTypes, metadata.UserDefaults)], native.Quantifier, native.Delay, [.. native.VariableIds]);
    }

    // The frame pads a short source list with the defaults, as WiredLegacyProtocol.Write does.
    private static IEnumerable<int> Padded(IEnumerable<int> sources, IEnumerable<int> defaults)
    {
        var given = sources.ToArray();
        var fill = defaults.ToArray();

        return given.Length > 0 && given.Length < fill.Length ? given.Concat(fill.Skip(given.Length)) : given;
    }
}
