using Lss.App.ViewModels;

namespace Lss.Tests;

public sealed class MyGamesViewModelTests : IDisposable
{
    private const string Second = "b2c3d4e5";

    private readonly TestFolder folder = new();

    public MyGamesViewModelTests() => TestApp.Ensure();

    public void Dispose() => folder.Dispose();

    private (SaveManager Manager, Person Player) Setup()
    {
        SaveManager manager = folder.CreateManager("A1");
        TestFolder.WritePeople(folder.Data,
            $"Ordine={TestFolder.Owner},{Second}\n" +
            $"{TestFolder.Owner}.Nome=Andrea\n{TestFolder.Owner}.Ruolo=admin\n" +
            $"{Second}.Nome=Clara\n{Second}.Ruolo=utente\n");
        var people = new People(folder.Data);
        people.Load();
        manager.Players = people;
        return (manager, people.Find(TestFolder.Owner)!);
    }

    [Fact]
    public void TheFavouriteComesFirst()
    {
        var (manager, player) = Setup();
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        manager.CreateProfile("Peach", false, TestFolder.Owner);
        manager.AddPlayTime("Profilo_Peach", DateTime.Now.AddHours(-1), DateTime.Now);

        var model = new MyGamesViewModel(manager, player);

        Assert.Equal(new[] { "Profilo_Mario", "Profilo_Peach" }, model.Rows.Select(row => row.ProfileName));
        Assert.True(model.Rows[0].IsFavourite);
    }

    [Fact]
    public void ChoosingARowGivesItsPlaythrough()
    {
        var (manager, player) = Setup();
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        manager.CreateProfile("Peach", false, TestFolder.Owner);
        var model = new MyGamesViewModel(manager, player);

        model.Choose(model.Rows[1]);

        Assert.Equal("Profilo_Peach", model.Choice!.ProfileName);
    }

    [Fact]
    public void StartingANewGameGivesNoPlaythrough()
    {
        var (manager, player) = Setup();
        var model = new MyGamesViewModel(manager, player);

        model.ChooseNew();

        Assert.NotNull(model.Choice);
        Assert.Null(model.Choice!.ProfileName);
    }

    [Fact]
    public void OnlyThePlayersOwnPlaythroughsAreListed()
    {
        var (manager, player) = Setup();
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        manager.CreateProfile("Luigi", false, Second);

        var model = new MyGamesViewModel(manager, player);

        Assert.Equal(new[] { "Profilo_Mario" }, model.Rows.Select(row => row.ProfileName));
    }
}
