using Lss.App.ViewModels;

namespace Lss.Tests;

public sealed class NewPlaythroughPlayerTests : IDisposable
{
    private const string Admin = "a1b2c3d4";
    private const string Second = "b2c3d4e5";

    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    private IReadOnlyList<Person> TwoPeople()
    {
        TestFolder.WritePeople(folder.Data,
            $"Ordine={Admin},{Second}\n" +
            $"{Admin}.Nome=Andrea\n{Admin}.Ruolo=admin\n" +
            $"{Second}.Nome=Clara\n{Second}.Ruolo=utente\n");

        var people = new People(folder.Data);
        people.Load();
        return people.All;
    }

    [Fact]
    public void TheOwnerOfThePlaythroughInPlayIsPreselected()
    {
        var model = new NewPlaythroughViewModel(_ => false, players: TwoPeople(), defaultPlayerId: Second);

        Assert.True(model.ShowPlayer);
        Assert.Equal(Second, model.SelectedPlayer!.Id);
        Assert.Equal(Second, model.CreateResult().PlayerId);
    }

    [Fact]
    public void TheChosenPlayerIsTheResult()
    {
        var model = new NewPlaythroughViewModel(_ => false, players: TwoPeople(), defaultPlayerId: Admin);

        model.SelectedPlayer = model.Players[1];
        model.Name = "Peach";

        Assert.Equal(Second, model.CreateResult().PlayerId);
    }

    [Fact]
    public void WithOnePlayerTheFieldIsHidden()
    {
        var model = new NewPlaythroughViewModel(_ => false, players: TwoPeople().Take(1).ToList(), defaultPlayerId: Admin);

        Assert.False(model.ShowPlayer);
        Assert.Equal(Admin, model.CreateResult().PlayerId);
    }

    [Fact]
    public void ARenameHasNoPlayerField()
    {
        var model = new NewPlaythroughViewModel(_ => false, "Mario", TwoPeople(), Admin);

        Assert.False(model.ShowPlayer);
    }
}
