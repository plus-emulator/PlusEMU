using System.Reflection;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private const uint BuildersClubVariableId = 994;

    // @event.variable_update.box_id is the id the client sees for a Builders Club variable box.
    [Fact]
    public void TheBoxIdOfAVariableChangeIsTheBuildersClubIdTheClientSees()
    {
        var wire = BuildersClubItemIds.Assign(BuildersClubVariableId);

        try {
            var prepared = PrepareSpeech(new SpeechClock());
            var module = new WiredVariableModule(RoomId, new BuildersClubVariableDirectory(), new MemoryWiredVariableStore(), TimeProvider.System,
                new RoomWiredBuiltinVariables(_room, prepared.Wired.ReadBuiltin));
            typeof(WiredRoomVariables).GetField("<Module>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(prepared.Wired.Variables, module);
            AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "vg1" }, 0);
            AddVariableBox(302, "wf_act_give_var", [1, 0, 5, 1, 0, -10, 0], [$"user:{BuildersClubVariableId}", "n"], 1, 1, 1);
            AddVariableBox(311, "wf_trg_var_changed", [1, 1, 1, 7, -1], [$"user:{BuildersClubVariableId}"], 0, 2, 2);
            AddSpeechBox(312, "wf_act_show_message", new() { IntParams = [0, 1, 34, -1], Text = "box=$(b)" }, 1, 2, 2);
            var output = WiredBox(313, "wf_xtra_text_output_variable", 2, 2);
            output.Item.GetZ = 2;
            WiredNativeTestSupport.Install(output, WiredNativeEditorProjection.DefaultNative(output.Descriptor) with
            {
                OwnedIntParams = [0, -20, 0],
                VariableIds = ["-459"],
                Text = "b"
            });
            Assert.True(_room.GetWired().AddBox(output));
            _client.Packets.Clear();

            prepared.Actor.OnChat(0, "vg1", false);

            Assert.Equal(["vg1", $"box={wire}"], SpeechMessages(ServerPacketHeader.ChatComposer));
        }
        finally {
            BuildersClubItemIds.Release(BuildersClubVariableId);
        }
    }

    private sealed class BuildersClubVariableDirectory : IWiredVariableDirectory
    {
        public uint? GetRoomOwner(uint roomId) => 7;
        public WiredVariableDefinition? Find(uint id) => id == BuildersClubVariableId
            ? new(id, RoomId, 7, "score", WiredVariableTarget.User, WiredVariableAvailability.RoomActive, true) : null;
    }
}
