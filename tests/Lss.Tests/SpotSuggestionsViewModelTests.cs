using Lss.App.ViewModels;

namespace Lss.Tests;

/// <summary>
/// Finestra delle proposte dopo una partita (N11): aggiungi, non sono salvataggi (per
/// questo gioco o per tutti), più tardi, chiudi. Usa una cartella dati di prova
/// (AppData.Root, per le cartelle ignorate) e dialoghi finti.
/// </summary>
[Collection("AppData")]
public sealed class SpotSuggestionsViewModelTests : IDisposable
{
    private readonly TestFolder folder = new();
    private readonly FakeDialogService dialogs = new();
    private readonly string savedDataRoot = AppData.Root;

    public SpotSuggestionsViewModelTests()
    {
        AppData.Root = folder.Data;
        TestApp.Ensure();
    }

    public void Dispose()
    {
        AppData.Root = savedDataRoot;
        folder.Dispose();
    }

    private string Extra => Path.Combine(folder.Root, "extra");

    /// <summary>Una proposta in proposte.ini, con un motivo come quelli che la ricerca salva.</summary>
    private void SuggestExtra()
    {
        TestFolder.Write(Path.Combine(Extra, "a.sav"), "x");
        IniFile.Write(Path.Combine(folder.Data, "proposte.ini"),
        [
            new("Proposta.0", string.Join("|", Extra, "", "9", DateTime.UtcNow.ToString("O"), "GameName:6:Hollow")),
        ]);
    }

    private GameViewModel Game() => new(folder.CreateManager("L"), dialogs);

    // File sparsi: "per tutti i giochi" non c'è, e "non sono salvataggi" non ignora la cartella.
    [Fact]
    public void LooseFilesCanOnlyBeDismissedForThisGame()
    {
        TestFolder.Write(Path.Combine(Extra, "a.sav"), "x");
        IniFile.Write(Path.Combine(folder.Data, "proposte.ini"),
        [
            new("Proposta.0", string.Join("|", Extra, "*.sav", "9", DateTime.UtcNow.ToString("O"), "")),
        ]);
        GameViewModel game = Game();
        var vm = new SpotSuggestionsViewModel(game, SessionScan.Pending(folder.Data));

        SpotSuggestionItem item = Assert.Single(vm.Items);
        Assert.False(item.CanIgnoreForAll);

        game.IgnoreSuggestion(item.Suggestion, forAll: false);

        Assert.Empty(new IgnoredFolders(folder.Data).ForGame(game.Manager.DataFolder));
        Assert.Contains((Extra, "*.sav"), SessionScan.Dismissed(game.Manager.DataFolder));
    }

    [Fact]
    public void TheReasonsAreShownWithTheSuggestion()
    {
        SuggestExtra();
        GameViewModel game = Game();
        var vm = new SpotSuggestionsViewModel(game, SessionScan.Pending(folder.Data));

        SpotSuggestionItem item = Assert.Single(vm.Items);
        Assert.True(item.HasReasons);
        Assert.Contains("Hollow", item.ReasonsText);
        Assert.Equal(Extra, item.FolderText);
    }

    [Fact]
    public async Task AddPutsTheFolderInTheSpotsAndForgetsIt()
    {
        SuggestExtra();
        GameViewModel game = Game();
        var vm = new SpotSuggestionsViewModel(game, SessionScan.Pending(folder.Data));
        bool? finished = null;
        vm.Finished += done => finished = done;
        dialogs.ConfirmAnswer = true;

        await vm.Items[0].AddCommand.ExecuteAsync(null);

        Assert.Contains(game.Manager.Game.Spots, spot => spot.Folder == Extra);
        Assert.Empty(SessionScan.Pending(folder.Data));
        Assert.Equal(0, game.PendingSuggestions);
        Assert.Empty(vm.Items);
        Assert.True(finished);
    }

    [Fact]
    public async Task AddRefusedInTheChoiceKeepsTheSuggestion()
    {
        SuggestExtra();
        GameViewModel game = Game();
        var vm = new SpotSuggestionsViewModel(game, SessionScan.Pending(folder.Data));
        dialogs.ConfirmAnswer = false;

        await vm.Items[0].AddCommand.ExecuteAsync(null);

        Assert.DoesNotContain(game.Manager.Game.Spots, spot => spot.Folder == Extra);
        Assert.Single(SessionScan.Pending(folder.Data));
        Assert.Single(vm.Items);
    }

    [Fact]
    public void LaterKeepsTheSuggestion()
    {
        SuggestExtra();
        var vm = new SpotSuggestionsViewModel(Game(), SessionScan.Pending(folder.Data));

        vm.Items[0].LaterCommand.Execute(null);

        Assert.Empty(vm.Items);
        Assert.Single(SessionScan.Pending(folder.Data));
    }

    [Fact]
    public void NotSavesForThisGameIsNotSuggestedAgainForIt()
    {
        SuggestExtra();
        var vm = new SpotSuggestionsViewModel(Game(), SessionScan.Pending(folder.Data));
        SpotSuggestionItem item = vm.Items[0];

        item.ToggleIgnoreCommand.Execute(null);
        Assert.True(item.ChoosingIgnore);
        item.IgnoreForGameCommand.Execute(null);

        Assert.Empty(SessionScan.Pending(folder.Data));
        Assert.Contains(new IgnoredFolders(folder.Data).ForGame(folder.Data), ignored => ignored.Folder == Extra);
        Assert.Empty(new IgnoredFolders(folder.Data).Global);
    }

    [Fact]
    public void NotSavesForEveryGameIsNotSuggestedAgain()
    {
        SuggestExtra();
        var vm = new SpotSuggestionsViewModel(Game(), SessionScan.Pending(folder.Data));

        vm.Items[0].ToggleIgnoreCommand.Execute(null);
        vm.Items[0].IgnoreForAllCommand.Execute(null);

        Assert.Empty(SessionScan.Pending(folder.Data));
        IgnoredFolders ignored = new(folder.Data);
        ignored.Load();
        IgnoredFolder global = Assert.Single(ignored.Global);
        Assert.Equal("", global.GameKey);
    }

    [Fact]
    public void CloseChangesNothing()
    {
        SuggestExtra();
        GameViewModel game = Game();
        var vm = new SpotSuggestionsViewModel(game, SessionScan.Pending(folder.Data));
        bool? finished = null;
        vm.Finished += done => finished = done;

        vm.CloseCommand.Execute(null);

        Assert.False(finished);
        Assert.Single(SessionScan.Pending(folder.Data));
        Assert.DoesNotContain(game.Manager.Game.Spots, spot => spot.Folder == Extra);
    }
}
