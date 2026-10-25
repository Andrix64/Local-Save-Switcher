using Lss.App.ViewModels;

namespace Lss.Tests;

public sealed class AdminPickerViewModelTests : IDisposable
{
    private const string Admin = "a1b2c3d4";
    private const string User = "b2c3d4e5";

    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    /// <summary>Persone: Andrea (admin, colore arancio) e Clara (utente).</summary>
    private People People()
    {
        TestFolder.Write(Path.Combine(folder.Data, "persone.ini"),
            $"Ordine={Admin},{User}\n" +
            $"{Admin}.Nome=Andrea\n{Admin}.Colore=#C2410C\n{Admin}.Ruolo=admin\n" +
            $"{User}.Nome=Clara\n{User}.Colore=#1D6FD0\n{User}.Ruolo=utente\n");

        var people = new People(folder.Data);
        people.Load();
        return people;
    }

    [Fact]
    public void OnlyAdminsAreOffered()
    {
        var picker = new AdminPickerViewModel(People());

        AdminTile tile = Assert.Single(picker.Tiles);
        Assert.Equal(Admin, tile.Person.Id);
        Assert.Equal("A", tile.Initial);
    }

    [Fact]
    public void ChoosingAnAdminSetsTheResult()
    {
        var picker = new AdminPickerViewModel(People());
        Person andrea = picker.Tiles[0].Person;

        Assert.True(picker.Choose(andrea));

        Assert.Equal(andrea, picker.Chosen);
    }

    [Fact]
    public void ChoosingAUserIsRefused()
    {
        var people = People();
        var picker = new AdminPickerViewModel(people);

        Assert.False(picker.Choose(people.Find(User)!));

        Assert.Null(picker.Chosen);
    }

    [Fact]
    public void CancelingLeavesNothingChosen()
    {
        var picker = new AdminPickerViewModel(People());

        Assert.Null(picker.Chosen);
    }
}
