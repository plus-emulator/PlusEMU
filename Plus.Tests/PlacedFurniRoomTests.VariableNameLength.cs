using Xunit;

namespace Plus.Tests;

/// <summary>
/// A user, furni, global or context variable name is cleaned first and the 1 to 40 character limit applies to the cleaned name, like a
/// pointer name. Every box is saved from its raw packet through the real save event.
/// </summary>
public partial class PlacedFurniRoomTests
{
    private static readonly (uint Id, string Own)[] NamedPlainKinds = [(NamedUser, "nu"), (NamedFurni, "nf"), (NamedGlobal, "ng"), (NamedContext, "nc")];

    [Theory]
    [InlineData(40, 1, 0, "a")]
    [InlineData(0, 1, 40, "a")]
    [InlineData(20, 1, 20, "a")]
    [InlineData(80, 1, 0, "a")]
    public void AVariableNameIsCleanedBeforeItsLengthIsChecked(int before, int letters, int after, string stored)
    {
        var service = NamedVariableRoom();
        var name = new string('_', before) + new string('a', letters) + new string('_', after);

        foreach (var (id, own) in NamedPlainKinds) {
            Assert.Equal("saved", SaveNamed(service, id, name));
            Assert.Equal(stored, StoredName(id));
            Assert.Equal("saved", SaveNamed(service, id, own));
        }
    }

    [Theory]
    [InlineData("                                        a", "saved", "a")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", NameLength, null)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", NameLength, null)]
    [InlineData("a                                       b", NameLength, null)]
    public void OnlyACleanedVariableNameOfMoreThanFortyCharactersIsTooLong(string name, string answer, string? stored)
    {
        var service = NamedVariableRoom();

        foreach (var (id, own) in NamedPlainKinds) {
            Assert.Equal(answer, SaveNamed(service, id, name));
            Assert.Equal(stored ?? own, StoredName(id));
            Assert.Equal("saved", SaveNamed(service, id, own));
        }
    }
}
