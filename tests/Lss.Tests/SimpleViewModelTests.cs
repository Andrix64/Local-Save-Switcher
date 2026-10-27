using Lss.App.ViewModels;

namespace Lss.Tests;

public sealed class SimpleViewModelTests : IDisposable
{
    private const string Second = "b2c3d4e5";

    private readonly TestFolder folder = new();

    private readonly List<string> played = [];
    private readonly List<string> setUp = [];
    private int added;

    public SimpleViewModelTests() => TestApp.Ensure();

    public void Dispose() => folder.Dispose();

    /// <summary>Manager con due persone: TestFolder.Owner ("Andrea", admin) e Second ("Clara", utente).</summary>
    private (SaveManager Manager, People People) ManagerWithTwoPlayers(string dataFolder)
    {
        SaveManager manager = new SaveManager(folder.Game, dataFolder, "", null, new FakeProvider());
        TestFolder.Write(Path.Combine(dataFolder, "persone.ini"),
            $"Ordine={TestFolder.Owner},{Second}\n" +
            $"{TestFolder.Owner}.Nome=Andrea\n{TestFolder.Owner}.Ruolo=admin\n" +
            $"{Second}.Nome=Clara\n{Second}.Ruolo=utente\n");

        var people = new People(dataFolder);
        people.Load();
        manager.Players = people;
        return (manager, people);
    }

    private static GameItemViewModel Item(string name, SupportState support)
    {
        var game = new GameInfo("fake", name, name, "", "", "", [], support, Source: SaveSource.None);
        return new GameItemViewModel(game, new GameAccount("fake", "x", "Fake", "", null), new FakeProvider());
    }

    private SimpleViewModel Model(SaveManager manager, People people, FakeDialogService dialogs,
        params GameItemViewModel[] games) =>
        new(people, dialogs, games, _ => manager,
            item => { setUp.Add(item.Name); return Task.CompletedTask; },
            item => { played.Add(item.Name); return Task.CompletedTask; },
            () => { added++; return Task.CompletedTask; });

    [Fact]
    public void PlayersAreInThePeopleOrder()
    {
        (SaveManager manager, People people) = ManagerWithTwoPlayers(folder.Data);
        SimpleViewModel model = Model(manager, people, new FakeDialogService());

        Assert.Equal(new[] { "Andrea", "Clara" }, model.Players.Select(tile => tile.Person.Name));
        Assert.True(model.Players[0].Person.IsAdmin);
        Assert.False(model.OnGamesPage);
    }

    [Fact]
    public void UnsafeGamesAreNotShownAndNotReadyOnesAreFaded()
    {
        (SaveManager manager, People people) = ManagerWithTwoPlayers(folder.Data);
        SimpleViewModel model = Model(manager, people, new FakeDialogService(),
            Item("Mario", SupportState.Supported),
            Item("Zelda", SupportState.NotFound),
            Item("Bad", SupportState.Unsafe));

        model.SelectPlayer(people.Find(Second)!);

        Assert.Equal(2, model.Games.Count);
        Assert.True(model.Games.Single(tile => tile.Name == "Mario").Ready);
        Assert.False(model.Games.Single(tile => tile.Name == "Zelda").Ready);
    }

    [Fact]
    public void GamesWithTheLastPlayFirst()
    {
        (SaveManager manager, People people) = ManagerWithTwoPlayers(folder.Data);
        string otherData = Path.Combine(folder.Root, "dati2");
        SaveManager other = new(folder.Game, otherData, "", null, new FakeProvider());
        other.CreateProfile("Luigi", false, TestFolder.Owner);
        other.AddPlayTime("Profilo_Luigi", DateTime.Now.AddHours(-1), DateTime.Now);

        // Zork ha l'ultima partita di Andrea (nel suo gestore), Aardvark non l'ha mai giocato.
        GameItemViewModel aardvark = Item("Aardvark", SupportState.Supported);
        GameItemViewModel zork = Item("Zork", SupportState.Supported);
        var model = new SimpleViewModel(people, new FakeDialogService(), [aardvark, zork],
            item => item == zork ? other : manager,
            _ => Task.CompletedTask, _ => Task.CompletedTask, () => Task.CompletedTask);
        model.SelectPlayer(people.Find(TestFolder.Owner)!);

        Assert.Equal(new[] { "Zork", "Aardvark" }, model.Games.Select(tile => tile.Name));
    }

    [Fact]
    public async Task AUserNeedsAnAdminToSetUpAGame()
    {
        (SaveManager manager, People people) = ManagerWithTwoPlayers(folder.Data);
        var dialogs = new FakeDialogService { ChosenAdmin = null };
        SimpleViewModel model = Model(manager, people, dialogs, Item("Zelda", SupportState.NotFound));
        model.SelectPlayer(people.Find(Second)!);

        await model.ActivateAsync(model.Games.Single());

        Assert.Contains("Admin", dialogs.Calls);
        Assert.Empty(setUp);
    }

    [Fact]
    public async Task AnAdminChosenSetsUpAGame()
    {
        (SaveManager manager, People people) = ManagerWithTwoPlayers(folder.Data);
        var dialogs = new FakeDialogService { ChosenAdmin = people.Find(TestFolder.Owner) };
        SimpleViewModel model = Model(manager, people, dialogs, Item("Zelda", SupportState.NotFound));
        model.SelectPlayer(people.Find(Second)!);

        await model.ActivateAsync(model.Games.Single());

        Assert.Equal(new[] { "Zelda" }, setUp);
    }

    [Fact]
    public async Task AReadyGameIsPlayedWithoutAsking()
    {
        (SaveManager manager, People people) = ManagerWithTwoPlayers(folder.Data);
        var dialogs = new FakeDialogService();
        SimpleViewModel model = Model(manager, people, dialogs, Item("Mario", SupportState.Supported));
        model.SelectPlayer(people.Find(Second)!);

        await model.ActivateAsync(model.Games.Single());

        Assert.Equal(new[] { "Mario" }, played);
        Assert.Empty(dialogs.Calls);
    }

    [Fact]
    public async Task AddingAPlayerNeedsAnAdminAndMakesAUser()
    {
        (SaveManager manager, People people) = ManagerWithTwoPlayers(folder.Data);
        var dialogs = new FakeDialogService { ChosenAdmin = people.Find(TestFolder.Owner), PromptAnswer = "Luca" };
        SimpleViewModel model = Model(manager, people, dialogs);

        await model.AddPlayerCommand.ExecuteAsync(null);

        Assert.Equal(3, model.Players.Count);
        Person luca = model.Players.Single(tile => tile.Person.Name == "Luca").Person;
        Assert.False(luca.IsAdmin);
    }

    [Fact]
    public async Task AddingAGameAsAUserWithoutAnAdminDoesNothing()
    {
        (SaveManager manager, People people) = ManagerWithTwoPlayers(folder.Data);
        var dialogs = new FakeDialogService { ChosenAdmin = null };
        SimpleViewModel model = Model(manager, people, dialogs);
        model.SelectPlayer(people.Find(Second)!);

        await model.AddGameCommand.ExecuteAsync(null);

        Assert.Equal(0, added);
    }

    [Fact]
    public async Task ARunningGameIsNotStarted()
    {
        // Il gioco è aperto: la piastrella è disattivata e un clic non fa niente.
        SaveManager manager = new(folder.Game, folder.Data, "", null, new FakeProvider { Running = true });
        TestFolder.Write(Path.Combine(folder.Data, "persone.ini"),
            $"Ordine={TestFolder.Owner}\n{TestFolder.Owner}.Nome=Andrea\n{TestFolder.Owner}.Ruolo=admin\n");
        var people = new People(folder.Data);
        people.Load();
        manager.Players = people;
        SimpleViewModel model = Model(manager, people, new FakeDialogService(), Item("Mario", SupportState.Supported));
        model.SelectPlayer(people.Find(TestFolder.Owner)!);

        SimpleGameTile tile = model.Games.Single();
        Assert.True(tile.Running);

        await model.ActivateAsync(tile);

        Assert.Empty(played);
    }

    [Fact]
    public void ChangePlayerGoesBackToThePlayers()
    {
        (SaveManager manager, People people) = ManagerWithTwoPlayers(folder.Data);
        SimpleViewModel model = Model(manager, people, new FakeDialogService());
        model.SelectPlayer(people.Find(Second)!);

        model.ChangePlayer();

        Assert.False(model.OnGamesPage);
    }

    [Fact]
    public async Task APlayerWithAPinMustTypeItToBePicked()
    {
        (SaveManager manager, People people) = ManagerWithTwoPlayers(folder.Data);
        people.SetPin(Second, "1234");
        var dialogs = new FakeDialogService { PinCheckAnswer = false };
        SimpleViewModel model = Model(manager, people, dialogs);

        await model.SelectPlayerAsync(people.Find(Second)!);

        Assert.Null(model.Current);
        Assert.Contains("CheckPin: Clara", dialogs.Calls);

        dialogs.PinCheckAnswer = true;
        await model.SelectPlayerAsync(people.Find(Second)!);

        Assert.Equal(Second, model.Current?.Id);
        Assert.True(model.OnGamesPage);
    }

    [Fact]
    public async Task APlayerWithoutAPinIsPickedWithoutAsking()
    {
        (SaveManager manager, People people) = ManagerWithTwoPlayers(folder.Data);
        var dialogs = new FakeDialogService();
        SimpleViewModel model = Model(manager, people, dialogs);

        await model.SelectPlayerAsync(people.Find(Second)!);

        Assert.Equal(Second, model.Current?.Id);
        Assert.DoesNotContain(dialogs.Calls, call => call.StartsWith("CheckPin"));
    }

    [Fact]
    public async Task AnAdminWhoTypedThePinIsNotAskedAgainUntilTheyGoBack()
    {
        (SaveManager manager, People people) = ManagerWithTwoPlayers(folder.Data);
        people.SetPin(TestFolder.Owner, "1234");
        var dialogs = new FakeDialogService { PinCheckAnswer = true, ChosenAdmin = null };
        SimpleViewModel model = Model(manager, people, dialogs);

        await model.SelectPlayerAsync(people.Find(TestFolder.Owner)!);
        await model.AddGameCommand.ExecuteAsync(null);

        // Il PIN è già stato messo: "Aggiungi un gioco" non lo chiede di nuovo.
        Assert.Equal(1, added);
        Assert.DoesNotContain("Admin", dialogs.Calls);

        // Tornando a "Chi gioca?" si chiude: la volta dopo il PIN si chiede (due volte in tutto)
        // e senza averlo messo l'azione chiede di nuovo l'admin.
        model.ChangePlayer();
        Assert.False(model.Unlocked);
        model.SelectPlayer(people.Find(TestFolder.Owner)!);
        await model.AddGameCommand.ExecuteAsync(null);

        Assert.Equal(1, added);
        Assert.Contains("Admin", dialogs.Calls);

        model.ChangePlayer();
        await model.SelectPlayerAsync(people.Find(TestFolder.Owner)!);

        Assert.Equal(2, dialogs.Calls.Count(call => call == "CheckPin: Andrea"));
    }
}
