namespace Lss.Tests;

public sealed class SimplePlayTests : IDisposable
{
    private const string Second = "b2c3d4e5";

    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    /// <summary>Risposta finta alla domanda "di chi sono i salvataggi?".</summary>
    private sealed class Asker(Person? answer) : ISimpleQuestions
    {
        public int Asked { get; private set; }

        public Task<Person?> WhoseSavesAsync(string gameName, IReadOnlyList<Person> people)
        {
            Asked++;
            return Task.FromResult(answer);
        }
    }

    /// <summary>Manager con due persone: TestFolder.Owner ("Andrea", admin) e Second ("Clara").</summary>
    private SaveManager ManagerWithTwoPlayers()
    {
        SaveManager manager = folder.CreateManager("A1");
        TestFolder.WritePeople(folder.Data,
            $"Ordine={TestFolder.Owner},{Second}\n" +
            $"{TestFolder.Owner}.Nome=Andrea\n{TestFolder.Owner}.Ruolo=admin\n" +
            $"{Second}.Nome=Clara\n{Second}.Ruolo=utente\n");

        var people = new People(folder.Data);
        people.Load();
        manager.Players = people;
        return manager;
    }

    private static Person Player(SaveManager manager, string id) => manager.Players!.Find(id)!;

    [Fact]
    public async Task TheFavouriteIsActivated()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        manager.CreateProfile("Peach", false, TestFolder.Owner);
        Assert.True(manager.ActivateProfile("Profilo_Peach").Success);
        var asker = new Asker(null);

        Result result = await SimplePlay.PrepareAsync(manager, manager.Players!, Player(manager, TestFolder.Owner), asker);

        Assert.True(result.Success, result.Message);
        Assert.Equal("Profilo_Mario", manager.State.ActiveProfile);
        Assert.Equal(0, asker.Asked);
    }

    [Fact]
    public async Task UnsavedProgressIsSavedBeforeSwitching()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        manager.CreateProfile("Peach", false, TestFolder.Owner);
        Assert.True(manager.ActivateProfile("Profilo_Peach").Success);
        TestFolder.Write(folder.SaveFile, "PROGRESSI");

        Result result = await SimplePlay.PrepareAsync(manager, manager.Players!, Player(manager, TestFolder.Owner), new Asker(null));

        Assert.True(result.Success, result.Message);
        Assert.Equal("PROGRESSI", File.ReadAllText(Path.Combine(manager.ProfilesPath, "Profilo_Peach", "remote", "slot.sav")));
    }

    [Fact]
    public async Task SavesOfAnotherPlayerBecomeTheirFavouriteAndThePlayerGetsANewOne()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        var asker = new Asker(Player(manager, Second));

        Result result = await SimplePlay.PrepareAsync(manager, manager.Players!, Player(manager, TestFolder.Owner), asker);

        Assert.True(result.Success, result.Message);
        Assert.Equal(1, asker.Asked);
        Assert.Equal("Profilo_Clara", manager.FavouriteOf(Second));
        Assert.Equal("Profilo_Andrea", manager.FavouriteOf(TestFolder.Owner));
        Assert.Equal("Profilo_Andrea", manager.State.ActiveProfile);
    }

    [Fact]
    public async Task SavesOfThePlayerBecomeTheirFavouriteAndAreActive()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        var asker = new Asker(Player(manager, TestFolder.Owner));

        Result result = await SimplePlay.PrepareAsync(manager, manager.Players!, Player(manager, TestFolder.Owner), asker);

        Assert.True(result.Success, result.Message);
        Assert.Equal("Profilo_Andrea", manager.FavouriteOf(TestFolder.Owner));
        Assert.Equal("Profilo_Andrea", manager.State.ActiveProfile);
        Assert.Single(manager.GetProfiles());
    }

    [Fact]
    public async Task SavesOfNobodyStayAsideAndThePlayerGetsANewFavourite()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        var asker = new Asker(null);

        Result result = await SimplePlay.PrepareAsync(manager, manager.Players!, Player(manager, TestFolder.Owner), asker);

        Assert.True(result.Success, result.Message);
        Assert.Equal(1, asker.Asked);
        string aside = SaveManager.ProfileFolderName(Strings.SavesBeforeLss);
        Assert.Equal(TestFolder.Owner, manager.GetInfo(aside).Owner);
        Assert.Equal("Profilo_Andrea", manager.FavouriteOf(TestFolder.Owner));
        Assert.NotEqual(aside, manager.FavouriteOf(TestFolder.Owner));
        Assert.Equal("Profilo_Andrea", manager.State.ActiveProfile);
    }

    [Fact]
    public async Task WithoutPlaythroughsAndWithoutSavesNothingIsAsked()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        File.Delete(folder.SaveFile);
        var asker = new Asker(null);

        Result result = await SimplePlay.PrepareAsync(manager, manager.Players!, Player(manager, TestFolder.Owner), asker);

        Assert.True(result.Success, result.Message);
        Assert.Equal(0, asker.Asked);
        Assert.Equal("Profilo_Andrea", manager.FavouriteOf(TestFolder.Owner));
    }

    [Fact]
    public async Task MyGamesChoiceBecomesTheFavouriteAndIsPlayed()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        manager.CreateProfile("Peach", false, TestFolder.Owner);

        Result result = await SimplePlay.PrepareWithAsync(manager, Player(manager, TestFolder.Owner), "Profilo_Peach");

        Assert.True(result.Success, result.Message);
        Assert.Equal("Profilo_Peach", manager.FavouriteOf(TestFolder.Owner));
        Assert.Equal("Profilo_Peach", manager.State.ActiveProfile);
    }

    [Fact]
    public async Task MyGamesRefusesAPlaythroughOfAnotherPlayer()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        manager.CreateProfile("Luigi", false, Second);

        Result result = await SimplePlay.PrepareWithAsync(manager, Player(manager, TestFolder.Owner), "Profilo_Luigi");

        Assert.False(result.Success);
        Assert.Equal("Profilo_Mario", manager.FavouriteOf(TestFolder.Owner));
    }

    [Fact]
    public async Task StartANewGameMakesANewFavourite()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        manager.CreateProfile("Mario", true, TestFolder.Owner);

        Result result = await SimplePlay.PrepareNewAsync(manager, Player(manager, TestFolder.Owner));

        Assert.True(result.Success, result.Message);
        // Il nome è libero ("Andrea"): la nuova partita diventa la preferita.
        Assert.Equal("Profilo_Andrea", manager.FavouriteOf(TestFolder.Owner));
        Assert.Equal(2, manager.GetProfilesOf(TestFolder.Owner).Count);
    }
}
