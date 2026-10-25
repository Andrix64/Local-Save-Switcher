using CommunityToolkit.Mvvm.Input;
using Lss.App.Services;
using Lss.App.ViewModels;

namespace Lss.Tests;

/// <summary>
/// Procedura "Trova i salvataggi" (M4): passi, orologio e ricerca finti, dialoghi
/// finti. AppData.Root punta a una cartella di test.
/// </summary>
[Collection("AppData")]
public sealed class LearnSavesViewModelTests : IDisposable
{
    private readonly TestFolder folder = new();
    private readonly FakeDialogService dialogs = new();
    private readonly FakeProvider provider = new() { Id = "steam" };
    private readonly string savedDataRoot = AppData.Root;
    private readonly DateTime start = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);
    private DateTime now;
    private DateTime? scannedSince;
    private LearnResult scanResult = new([], false);

    public LearnSavesViewModelTests()
    {
        AppData.Root = folder.Data;
        now = start;
    }

    public void Dispose()
    {
        AppData.Root = savedDataRoot;
        folder.Dispose();
    }

    private string Documents => Path.Combine(folder.Root, "home", "Documents");

    private string DataFolder => Path.Combine(folder.Data, "steam", "111", "123450");

    private static GameAccount Account => new("steam", "111", "Andrix", "", null);

    private GameInfo Game(string saveFolder = "") =>
        new("steam", "123450", "Sandfall", "", "", "", GameInfo.SpotsOf(saveFolder, SaveSource.None), SupportState.Unsure, "", "", SaveSource.None);

    private LearnSavesViewModel Model(GameInfo? game = null) => new(
        game ?? Game(), Account, provider, dialogs, () => now,
        (since, _) =>
        {
            scannedSince = since;
            return Task.FromResult(scanResult);
        });

    /// <summary>Una cartella con un file cambiato, come la trova la ricerca.</summary>
    private SaveCandidate Candidate(string name, int score = 9)
    {
        string path = Path.Combine(Documents, name);
        TestFolder.Write(Path.Combine(path, "user1.dat"), "x");
        var root = new SearchRoot(Documents, 6, false);
        var file = new ChangedFile(Path.Combine(path, "user1.dat"), 1, start, root);

        return new SaveCandidate(path, score, [file], [new ScoreReason(ScoreReasonKind.GameName, 6, name)]);
    }

    private static Task Run(IAsyncRelayCommand command) => command.ExecuteAsync(null);

    [Fact]
    public async Task StartLaunchesTheGameAndWaitsForItToClose()
    {
        LearnSavesViewModel model = Model();

        await Run(model.StartCommand);

        Assert.Single(provider.Launched);
        Assert.Equal(LearnStep.Playing, model.Step);
    }

    [Fact]
    public async Task GameAlreadyOpenIsNotStartedAgain()
    {
        provider.Running = true;
        LearnSavesViewModel model = Model();

        await Run(model.StartCommand);

        Assert.Empty(provider.Launched);
        Assert.Equal(LearnStep.Intro, model.Step);
        Assert.Equal(Strings.LearnCloseFirst.Replace("{0}", "Sandfall"), model.Message);
    }

    [Fact]
    public async Task ProviderThatCannotLaunchAsksForTheHandStart()
    {
        provider.CanLaunchGames = false;
        LearnSavesViewModel model = Model();

        Assert.Equal(Strings.LearnStartedByHand, model.StartText);

        await Run(model.StartCommand);

        Assert.Empty(provider.Launched);
        Assert.Equal(LearnStep.Playing, model.Step);
    }

    [Fact]
    public async Task GameThatWasOpenAndThenClosedIsLooked()
    {
        LearnSavesViewModel model = Model();
        await Run(model.StartCommand);

        provider.Running = true;
        await model.TickAsync();
        provider.Running = false;

        now = start.AddSeconds(1);
        await model.TickAsync();
        now = start.AddSeconds(3);
        await model.TickAsync();
        Assert.Equal(LearnStep.Playing, model.Step);

        now = start.AddSeconds(6);
        await model.TickAsync();

        Assert.Equal(LearnStep.Result, model.Step);
        Assert.Equal(start.AddSeconds(-5), scannedSince);
    }

    [Fact]
    public async Task QuitButtonLooksAtOnce()
    {
        LearnSavesViewModel model = Model();
        await Run(model.StartCommand);

        await Run(model.QuitDoneCommand);

        Assert.Equal(LearnStep.Result, model.Step);
        Assert.True(model.NothingFound);
    }

    [Fact]
    public async Task NothingFoundSaysSo()
    {
        LearnSavesViewModel model = Model();
        await Run(model.StartCommand);
        scanResult = new LearnResult([], false);

        await Run(model.QuitDoneCommand);

        Assert.True(model.NothingFound);
        Assert.Empty(model.Candidates);
    }

    [Fact]
    public async Task PartialResultIsMarked()
    {
        LearnSavesViewModel model = Model();
        await Run(model.StartCommand);
        scanResult = new LearnResult([Candidate("Sandfall")], true);

        await Run(model.QuitDoneCommand);

        Assert.True(model.Partial);
        Assert.Single(model.Candidates);
    }

    [Fact]
    public async Task FirstCandidateIsRecommendedAndSelected()
    {
        LearnSavesViewModel model = Model();
        await Run(model.StartCommand);
        scanResult = new LearnResult([Candidate("Sandfall"), Candidate("Other", 4)], false);

        await Run(model.QuitDoneCommand);

        Assert.True(model.Candidates[0].Recommended);
        Assert.True(model.Candidates[0].IsSelected);
        Assert.False(model.Candidates[1].Recommended);
        Assert.True(model.Candidates[1].Unlikely);
    }

    [Fact]
    public async Task UsingTheSelectedFolderSavesItAsLearned()
    {
        LearnSavesViewModel model = Model();
        await Run(model.StartCommand);
        scanResult = new LearnResult([Candidate("Sandfall")], false);
        await Run(model.QuitDoneCommand);
        dialogs.ConfirmAnswer = true;
        bool? finished = null;
        model.Finished += saved => finished = saved;

        await Run(model.UseSelectedCommand);

        var settings = new GameSaveSettings(DataFolder);
        settings.Load();
        Assert.Equal(model.Candidates[0].Folder, settings.Folder);
        Assert.Equal(SaveSource.Learned, settings.Origin);
        Assert.True(finished);
    }

    [Fact]
    public async Task DeclinedConfirmationSavesNothing()
    {
        LearnSavesViewModel model = Model();
        await Run(model.StartCommand);
        scanResult = new LearnResult([Candidate("Sandfall")], false);
        await Run(model.QuitDoneCommand);
        dialogs.ConfirmAnswer = false;

        await Run(model.UseSelectedCommand);

        Assert.False(File.Exists(Path.Combine(DataFolder, "posti.ini")));
    }

    [Fact]
    public async Task NotASaveFolderForThisGameIsHiddenAndUndoBringsItBack()
    {
        LearnSavesViewModel model = Model();
        await Run(model.StartCommand);
        scanResult = new LearnResult([Candidate("Sandfall"), Candidate("Other", 4)], false);
        await Run(model.QuitDoneCommand);
        string ignored = model.Candidates[0].Folder;

        model.Candidates[0].IgnoreForGameCommand.Execute(null);

        Assert.Single(model.Candidates);
        Assert.True(model.HasIgnoredNotice);
        Assert.Contains(new IgnoredFolders(folder.Data).ForGame(DataFolder), entry => entry.Folder == ignored);

        model.UndoIgnoredCommand.Execute(null);

        Assert.Equal(2, model.Candidates.Count);
        Assert.False(model.HasIgnoredNotice);
        Assert.Empty(new IgnoredFolders(folder.Data).ForGame(DataFolder));
    }

    [Fact]
    public async Task NotASaveFolderForEveryGameIsWrittenAndUndone()
    {
        LearnSavesViewModel model = Model();
        await Run(model.StartCommand);
        scanResult = new LearnResult([Candidate("Sandfall")], false);
        await Run(model.QuitDoneCommand);

        model.Candidates[0].IgnoreForAllCommand.Execute(null);

        Assert.Empty(model.Candidates);
        var ignored = new IgnoredFolders(folder.Data);
        ignored.Load();
        Assert.Single(ignored.Global);

        model.UndoIgnoredCommand.Execute(null);

        Assert.Single(model.Candidates);
        ignored.Load();
        Assert.Empty(ignored.Global);
    }

    [Fact]
    public async Task CurrentFolderCannotBeMarkedAsNotASave()
    {
        string current = Candidate("Sandfall").Folder;
        LearnSavesViewModel model = Model(Game(current));
        await Run(model.StartCommand);
        scanResult = new LearnResult([Candidate("Sandfall")], false);
        await Run(model.QuitDoneCommand);

        model.Candidates[0].IgnoreForGameCommand.Execute(null);

        Assert.Single(model.Candidates);
        Assert.Equal(Strings.IgnoreCurrentFolder.Replace("{0}", "Sandfall"), model.Message);
        Assert.Empty(new IgnoredFolders(folder.Data).ForGame(DataFolder));
    }

    [Fact]
    public async Task TryAgainReturnsToTheFirstStep()
    {
        LearnSavesViewModel model = Model();
        await Run(model.StartCommand);
        scanResult = new LearnResult([Candidate("Sandfall")], false);
        await Run(model.QuitDoneCommand);

        model.TryAgainCommand.Execute(null);

        Assert.Equal(LearnStep.Intro, model.Step);
        Assert.Empty(model.Candidates);
    }

    [Fact]
    public async Task LookInAnotherFolderIsRememberedAndGoesBack()
    {
        string other = Path.Combine(folder.Root, "other");
        dialogs.FolderToPick = other;
        LearnSavesViewModel model = Model();
        await Run(model.StartCommand);
        scanResult = new LearnResult([], false);
        await Run(model.QuitDoneCommand);

        await Run(model.LookElsewhereCommand);

        Assert.Equal(LearnStep.Intro, model.Step);
        Assert.Contains(other, new GameSaveSettings(DataFolder).ExtraFolders);
    }

    [Fact]
    public async Task ChoosingTheFolderMyselfSavesItAsChosen()
    {
        string mine = Path.Combine(folder.Root, "mine");
        TestFolder.Write(Path.Combine(mine, "slot.sav"), "x");
        dialogs.FolderToPick = mine;
        dialogs.ConfirmAnswer = true;
        LearnSavesViewModel model = Model();

        await Run(model.ChooseMyselfCommand);

        var settings = new GameSaveSettings(DataFolder);
        settings.Load();
        Assert.Equal(mine, settings.Folder);
        Assert.Equal(SaveSource.Chosen, settings.Origin);
    }

    [Fact]
    public void CancelBeforeLookingClosesTheWindow()
    {
        LearnSavesViewModel model = Model();
        bool? finished = null;
        model.Finished += saved => finished = saved;

        model.CancelCommand.Execute(null);

        Assert.False(finished);
    }
}
