namespace Lss.Tests;

public sealed class FavouritesTests : IDisposable
{
    private const string Second = "b2c3d4e5";

    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    /// <summary>Manager con due persone: TestFolder.Owner (admin) e Second (utente).</summary>
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

    /// <summary>Testo di preferite.ini, per confrontarlo prima e dopo un errore.</summary>
    private string FavouritesFile() => File.ReadAllText(Path.Combine(folder.Data, "preferite.ini"));

    /// <summary>Segna una sessione di gioco che finisce a "end": decide quale partita è l'ultima giocata.</summary>
    private static void PlayUntil(SaveManager manager, string profile, DateTime end) =>
        manager.AddPlayTime(profile, end.AddHours(-1), end);

    [Fact]
    public void FirstPlaythroughIsTheFavouriteOfItsPlayer()
    {
        SaveManager manager = ManagerWithTwoPlayers();

        manager.CreateProfile("Mario", true, TestFolder.Owner);
        manager.CreateProfile("Peach", false, TestFolder.Owner);

        Assert.Equal("Profilo_Mario", manager.FavouriteOf(TestFolder.Owner));
    }

    [Fact]
    public void EachPlayerHasOwnFavourite()
    {
        SaveManager manager = ManagerWithTwoPlayers();

        manager.CreateProfile("Mario", true, TestFolder.Owner);
        manager.CreateProfile("Luigi", false, Second);

        Assert.Equal("Profilo_Mario", manager.FavouriteOf(TestFolder.Owner));
        Assert.Equal("Profilo_Luigi", manager.FavouriteOf(Second));
    }

    [Fact]
    public void DeletingTheFavouriteMakesTheMostRecentOneTheFavourite()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        manager.CreateProfile("Peach", false, TestFolder.Owner);
        PlayUntil(manager, "Profilo_Mario", DateTime.Now.AddDays(-2));
        PlayUntil(manager, "Profilo_Peach", DateTime.Now.AddDays(-1));

        Assert.True(manager.DeleteProfile("Profilo_Mario").Success);

        Assert.Equal("Profilo_Peach", manager.FavouriteOf(TestFolder.Owner));
    }

    [Fact]
    public void DeletingTheLastPlaythroughLeavesNoFavourite()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        manager.CreateProfile("Mario", true, TestFolder.Owner);

        Assert.True(manager.DeleteProfile("Profilo_Mario").Success);

        Assert.Null(manager.FavouriteOf(TestFolder.Owner));
    }

    [Fact]
    public void RenamingKeepsTheFavourite()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        manager.CreateProfile("Peach", false, TestFolder.Owner);

        Assert.True(manager.RenameProfile("Profilo_Mario", "Bowser").Success);

        Assert.Equal("Profilo_Bowser", manager.FavouriteOf(TestFolder.Owner));
    }

    [Fact]
    public void GivingAwayTheFavouriteFixesBothPlayers()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        manager.CreateProfile("Peach", false, TestFolder.Owner);
        manager.CreateProfile("Luigi", false, Second);

        Assert.True(manager.SetOwner("Profilo_Mario", Second).Success);

        // Andrea ha ancora Peach, come preferita; Clara tiene Luigi e Mario non diventa sua preferita.
        Assert.Equal("Profilo_Peach", manager.FavouriteOf(TestFolder.Owner));
        Assert.Equal("Profilo_Luigi", manager.FavouriteOf(Second));
    }

    [Fact]
    public void GivingTheFavouriteToAPlayerWithNoneMakesItTheirs()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        manager.CreateProfile("Peach", false, TestFolder.Owner);

        Assert.True(manager.SetOwner("Profilo_Mario", Second).Success);

        Assert.Equal("Profilo_Mario", manager.FavouriteOf(Second));
        Assert.Equal("Profilo_Peach", manager.FavouriteOf(TestFolder.Owner));
    }

    [Fact]
    public void SetFavouriteOnlyTakesAPlaythroughOfThatPlayer()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        manager.CreateProfile("Luigi", false, Second);

        Assert.False(manager.SetFavourite(TestFolder.Owner, "Profilo_Luigi").Success);
        Assert.False(manager.SetFavourite(Second, "Profilo_Mario").Success);
        Assert.Equal("Profilo_Mario", manager.FavouriteOf(TestFolder.Owner));
        Assert.Equal("Profilo_Luigi", manager.FavouriteOf(Second));
    }

    [Fact]
    public void SetFavouriteChoosesAnotherPlaythroughOfTheSamePlayer()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        manager.CreateProfile("Peach", false, TestFolder.Owner);

        Assert.True(manager.SetFavourite(TestFolder.Owner, "Profilo_Peach").Success);

        Assert.Equal("Profilo_Peach", manager.FavouriteOf(TestFolder.Owner));
    }

    [Fact]
    public void FailedRenameLeavesTheFavouritesAsTheyWere()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        manager.CreateProfile("Peach", false, TestFolder.Owner);
        string before = FavouritesFile();

        Assert.False(manager.RenameProfile("Profilo_Mario", "Peach").Success);

        Assert.Equal(before, FavouritesFile());
        Assert.Equal("Profilo_Mario", manager.FavouriteOf(TestFolder.Owner));
    }

    [Fact]
    public void PlayingRecordsTheLastPlayOfThePlayer()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        manager.CreateProfile("Mario", true, TestFolder.Owner);

        PlayUntil(manager, "Profilo_Mario", DateTime.Now);

        Assert.Contains(TestFolder.Owner + ".Ultima=", FavouritesFile());
    }
}
