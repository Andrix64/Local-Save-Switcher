using Lss.App.ViewModels;

namespace Lss.Tests;

public sealed class PlayerGroupsTests : IDisposable
{
    private const string Second = "b2c3d4e5";

    private readonly TestFolder folder = new();

    public PlayerGroupsTests() => TestApp.Ensure();

    public void Dispose() => folder.Dispose();

    /// <summary>Manager con due persone: TestFolder.Owner ("Andrea") e Second ("Clara").</summary>
    private SaveManager ManagerWithTwoPlayers()
    {
        SaveManager manager = folder.CreateManager("A1");
        TestFolder.Write(Path.Combine(folder.Data, "persone.ini"),
            $"Ordine={TestFolder.Owner},{Second}\n" +
            $"{TestFolder.Owner}.Nome=Andrea\n{TestFolder.Owner}.Ruolo=admin\n" +
            $"{Second}.Nome=Clara\n{Second}.Ruolo=utente\n");

        var people = new People(folder.Data);
        people.Load();
        manager.Players = people;
        return manager;
    }

    private GameViewModel Game(SaveManager manager)
    {
        var game = new GameViewModel(manager, new FakeDialogService());
        game.Refresh();
        return game;
    }

    [Fact]
    public void GroupsFollowThePeopleOrder()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        manager.CreateProfile("Luigi", false, Second);

        GameViewModel game = Game(manager);

        Assert.Equal(new[] { "Andrea", "Clara" }, game.PlayerGroups.Select(group => group.Person.Name));
    }

    [Fact]
    public void TheFavouriteComesFirstInItsGroup()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        manager.CreateProfile("Peach", false, TestFolder.Owner);
        manager.AddPlayTime("Profilo_Peach", DateTime.Now.AddHours(-1), DateTime.Now);

        GameViewModel game = Game(manager);

        PlayerGroup andrea = game.PlayerGroups.Single(group => group.Person.Id == TestFolder.Owner);
        Assert.Equal("Profilo_Mario", andrea.Cards[0].ProfileName);
        Assert.True(andrea.Cards[0].IsFavourite);
        Assert.False(andrea.Cards[1].IsFavourite);
    }

    [Fact]
    public void PlayersWithoutPlaythroughsAreNotShown()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        manager.CreateProfile("Mario", true, TestFolder.Owner);

        GameViewModel game = Game(manager);

        PlayerGroup group = Assert.Single(game.PlayerGroups);
        Assert.Equal(TestFolder.Owner, group.Person.Id);
    }

    [Fact]
    public async Task TheStarMakesAnotherPlaythroughTheFavourite()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        manager.CreateProfile("Peach", false, TestFolder.Owner);
        GameViewModel game = Game(manager);
        PlaythroughViewModel peach = game.Playthroughs.Single(card => card.ProfileName == "Profilo_Peach");

        await game.SetFavouriteAsync(peach);

        Assert.Equal("Profilo_Peach", manager.FavouriteOf(TestFolder.Owner));
        Assert.Equal("Profilo_Peach", game.PlayerGroups[0].Cards[0].ProfileName);
    }

    [Fact]
    public async Task GiveToMovesThePlaythroughToAnotherPlayer()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        GameViewModel game = Game(manager);

        await game.GiveToAsync("Profilo_Mario", Second);

        PlayerGroup clara = game.PlayerGroups.Single(group => group.Person.Id == Second);
        Assert.Equal("Profilo_Mario", clara.Cards[0].ProfileName);
        Assert.Equal("Profilo_Mario", manager.FavouriteOf(Second));
    }

    [Fact]
    public void SaveFoldersStartClosedAndStayAsLeftForOtherGames()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        GameViewModel first = Game(manager);
        GameViewModel second = Game(manager);

        Assert.False(first.SaveFoldersOpen);

        first.SaveFoldersOpen = true;
        Assert.True(second.SaveFoldersOpen);

        first.SaveFoldersOpen = false;
    }
}
