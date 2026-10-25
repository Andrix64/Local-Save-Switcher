using Lss.App.ViewModels;

namespace Lss.Tests;

public sealed class GiveToTests : IDisposable
{
    private const string Second = "b2c3d4e5";

    private readonly TestFolder folder = new();

    public GiveToTests() => TestApp.Ensure();

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

    [Fact]
    public async Task TheMenuGivesThePlaythroughToTheChosenPlayer()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        var dialogs = new FakeDialogService { ChosenPerson = manager.Players!.Find(Second) };
        var game = new GameViewModel(manager, dialogs);

        await game.GiveToMenuAsync("Profilo_Mario");

        Assert.Equal(Second, manager.GetInfo("Profilo_Mario").Owner);
        Assert.Contains(dialogs.Calls, call => call.StartsWith("Choose"));
    }

    [Fact]
    public async Task CancelingTheMenuChangesNothing()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        var game = new GameViewModel(manager, new FakeDialogService { ChosenPerson = null });

        await game.GiveToMenuAsync("Profilo_Mario");

        Assert.Equal(TestFolder.Owner, manager.GetInfo("Profilo_Mario").Owner);
    }

    [Fact]
    public async Task WithoutAnotherPlayerNothingIsAsked()
    {
        SaveManager manager = folder.CreateManager("A1");
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        var dialogs = new FakeDialogService();
        var game = new GameViewModel(manager, dialogs);

        await game.GiveToMenuAsync("Profilo_Mario");

        Assert.Empty(dialogs.Calls);
        Assert.Equal(TestFolder.Owner, manager.GetInfo("Profilo_Mario").Owner);
    }

    [Fact]
    public void ThePickerOffersOnlyThePeopleInTheList()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        var picker = new PlayerChoiceViewModel("Chi?", "", manager.Players!.All.Where(p => p.Id == Second).ToList());

        Assert.Single(picker.Tiles);
        Assert.False(picker.Choose(manager.Players!.Find(TestFolder.Owner)!));
        Assert.Null(picker.Chosen);
        Assert.True(picker.Choose(manager.Players!.Find(Second)!));
        Assert.Equal(Second, picker.Chosen!.Id);
    }
}
