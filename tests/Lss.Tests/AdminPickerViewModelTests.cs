using Lss.App.ViewModels;

namespace Lss.Tests;

public sealed class AdminPickerViewModelTests : IDisposable
{
    private const string Admin = "a1b2c3d4";
    private const string User = "b2c3d4e5";

    private readonly TestFolder folder = new();

    public AdminPickerViewModelTests() => TestApp.Ensure();

    public void Dispose() => folder.Dispose();

    /// <summary>Persone: Andrea (admin, colore arancio) e Clara (utente).</summary>
    private People People()
    {
        TestFolder.WritePeople(folder.Data,
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

        PersonTile tile = Assert.Single(picker.Tiles);
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

    // Il bug segnalato: un admin senza PIN non deve passare da "Chi sei?".
    [Fact]
    public void ACurrentAdminWithoutPinIsAuthorisedWithoutAWindow()
    {
        People people = People();

        Assert.Equal(Admin, AdminPickerViewModel.WithoutWindow(people, people.Find(Admin))?.Id);
    }

    [Fact]
    public void TheOnlyAdminIsKnownWhenNobodyIsPlayingButAUserMustChoose()
    {
        People people = People();

        Assert.Equal(Admin, AdminPickerViewModel.KnownAdmin(people, null)?.Id);
        Assert.Null(AdminPickerViewModel.KnownAdmin(people, people.Find(User)));
    }

    [Fact]
    public void WithTwoAdminsAUserMustChoose()
    {
        People people = People();
        people.SetAdmin(User, true);

        Assert.Null(AdminPickerViewModel.KnownAdmin(people, null));
        Assert.Equal(User, AdminPickerViewModel.KnownAdmin(people, people.Find(User))?.Id);
    }

    [Fact]
    public void AKnownAdminWithPinGetsOnlyThePad()
    {
        People people = People();
        people.SetPin(Admin, "1234");

        Assert.Null(AdminPickerViewModel.WithoutWindow(people, people.Find(Admin)));

        var picker = new AdminPickerViewModel(people, AdminPickerViewModel.KnownAdmin(people, people.Find(Admin)));

        Assert.True(picker.OnlyPin);
        Assert.True(picker.PadOpen);
        Assert.Null(picker.Chosen);
    }
}
