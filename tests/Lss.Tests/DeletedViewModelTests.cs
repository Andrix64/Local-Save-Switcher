using Lss.App.ViewModels;

namespace Lss.Tests;

public sealed class DeletedViewModelTests : IDisposable
{
    private const string Second = "b2c3d4e5";

    private readonly TestFolder folder = new();

    public DeletedViewModelTests() => TestApp.Ensure();

    public void Dispose() => folder.Dispose();

    private SaveManager Manager()
    {
        SaveManager manager = folder.CreateManager("A1");
        manager.CreateProfile("Mario", false, TestFolder.Owner);
        manager.CreateProfile("Luigi", false, TestFolder.Owner);
        manager.DeleteProfile("Profilo_Mario");
        return manager;
    }

    [Fact]
    public async Task PurgeAsksAndDeletesTheSelected()
    {
        SaveManager manager = Manager();
        var dialogs = new FakeDialogService { ConfirmAnswer = true };
        var model = new DeletedViewModel(manager, dialogs);
        model.Start();
        model.Stop();
        model.Selected = model.Items.Single();

        await model.PurgeCommand.ExecuteAsync(null);

        Assert.Contains("Confirm: " + Strings.PurgeTitle, dialogs.Calls);
        Assert.Empty(manager.GetDeletedProfiles());
    }

    [Fact]
    public async Task CancelingThePurgeDeletesNothing()
    {
        SaveManager manager = Manager();
        var dialogs = new FakeDialogService { ConfirmAnswer = false };
        var model = new DeletedViewModel(manager, dialogs);
        model.Start();
        model.Stop();
        model.Selected = model.Items.Single();

        await model.PurgeCommand.ExecuteAsync(null);

        Assert.Single(manager.GetDeletedProfiles());
    }

    [Fact]
    public async Task PurgeAllNeedsTwoAndDeletesAll()
    {
        SaveManager manager = Manager();
        manager.DeleteProfile("Profilo_Luigi");
        var dialogs = new FakeDialogService { ConfirmAnswer = true };
        var model = new DeletedViewModel(manager, dialogs);
        model.Start();
        model.Stop();

        Assert.True(model.ShowPurgeAll);

        await model.PurgeAllCommand.ExecuteAsync(null);

        Assert.Empty(manager.GetDeletedProfiles());
    }

    [Fact]
    public void TheOwnerIsShownAndDashWhenThePlayerIsGone()
    {
        SaveManager manager = Manager();
        var model = new DeletedViewModel(manager, new FakeDialogService());
        model.Start();
        model.Stop();
        Assert.Equal("Test", model.Items.Single().OwnerText);

        // Il giocatore non esiste più: la riga dice "—".
        TestFolder.Write(Path.Combine(folder.Data, "persone.ini"),
            $"Ordine={Second}\n{Second}.Nome=Clara\n{Second}.Ruolo=admin\n");
        var people = new People(folder.Data);
        people.Load();
        manager.Players = people;

        model.Start();
        model.Stop();

        Assert.Equal("—", model.Items.Single().OwnerText);
    }
}
