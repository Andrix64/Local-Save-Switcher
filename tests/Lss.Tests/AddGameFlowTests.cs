using Lss.App.ViewModels;

namespace Lss.Tests;

/// <summary>
/// "Aggiungi un gioco": annullare la scelta del programma o il nome non aggiunge niente.
/// </summary>
public sealed class AddGameFlowTests : IDisposable
{
    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    private int GameCount() => ManualGames.ReadAll(folder.Data).Count;

    [Fact]
    public async Task CancellingTheProgramChoiceStopsBeforeTheName()
    {
        var dialogs = new FakeDialogService { FileToPick = null, PromptAnswer = "Gioco" };

        string? id = await new AddGameFlow(dialogs).AddAsync(folder.Data, folder.Root);

        Assert.Null(id);
        Assert.Equal(["PickFile"], dialogs.Calls);
        Assert.Equal(0, GameCount());
    }

    [Fact]
    public async Task CancellingTheNameAddsNothing()
    {
        var dialogs = new FakeDialogService { FileToPick = Path.Combine(folder.Root, "gioco.exe"), PromptAnswer = null };

        string? id = await new AddGameFlow(dialogs).AddAsync(folder.Data, folder.Root);

        Assert.Null(id);
        Assert.Equal(0, GameCount());
    }

    [Fact]
    public async Task ProgramAndNameAddTheGame()
    {
        string exe = Path.Combine(folder.Root, "gioco.exe");
        var dialogs = new FakeDialogService { FileToPick = exe, PromptAnswer = "  Il mio gioco " };

        string? id = await new AddGameFlow(dialogs).AddAsync(folder.Data, folder.Root);

        Assert.NotNull(id);
        ManualGame game = Assert.Single(ManualGames.ReadAll(folder.Data)).Game;
        Assert.Equal("Il mio gioco", game.Name);
        Assert.Equal(exe, game.Exe);
    }
}
