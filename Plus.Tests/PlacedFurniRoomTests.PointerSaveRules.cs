using Xunit;

namespace Plus.Tests;

/// <summary>
/// Pointer save rules. A pointer name is cleaned first (a space becomes an underscore, capitals are lowered, outer underscores go) and the
/// 1 to 40 character limit applies to the cleaned name. An Echo of a context variable is no accessor: the save is refused as a missing
/// variable and the pointer keeps its accessor. Every box is saved from its raw packet through the real save event.
/// </summary>
public partial class PlacedFurniRoomTests
{
    [Fact]
    public void AnEchoOfAContextVariableIsNoPointerAccessor()
    {
        var service = NamedVariableRoom();
        PlaceVariableBox(NamedEcho, "wf_var_echo");
        Assert.Equal("saved", SaveVariable(service, NamedEcho, [], "pecho", $"ctx:{NamedContext}"));

        Assert.Equal(MissingVariable, SaveVariable(service, NamedPointer, [1], "pu", $"ctx:{NamedEcho}"));
        Assert.Equal([$"ctx:{NamedContext}"], _variableBoxes[NamedPointer].Configuration.VariableIds.ToArray());
    }

    [Theory]
    [InlineData(40, 1, 0, "a")]
    [InlineData(0, 1, 40, "a")]
    [InlineData(20, 1, 20, "a")]
    [InlineData(80, 1, 0, "a")]
    public void APointerNameIsCleanedBeforeItsLengthIsChecked(int before, int letters, int after, string stored)
    {
        var service = NamedVariableRoom();
        var name = new string('_', before) + new string('a', letters) + new string('_', after);

        Assert.Equal("saved", SaveVariable(service, NamedPointer, [1], name, $"ctx:{NamedContext}"));
        Assert.Equal(stored, StoredName(NamedPointer));
    }

    [Theory]
    [InlineData("                                        a", "saved")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", NameLength)]
    [InlineData("a                                       b", NameLength)]
    public void OnlyACleanedPointerNameOfMoreThanFortyCharactersIsTooLong(string name, string answer)
    {
        var service = NamedVariableRoom();

        Assert.Equal(answer, SaveVariable(service, NamedPointer, [1], name, $"ctx:{NamedContext}"));
    }
}
