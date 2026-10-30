
using Lss.App.ViewModels;

namespace Lss.Tests;

/// <summary>
/// Le azioni sulle partite di un gioco: elenco, conferme, importazione.
/// Usano il dispatcher di Avalonia senza finestre (toast): vedi TestApp.
/// </summary>
public sealed class GameViewModelTests : IDisposable
{
    private readonly TestFolder folder = new();
    private readonly FakeDialogService dialogs = new();

    public GameViewModelTests() => TestApp.Ensure();

    public void Dispose() => folder.Dispose();

    /// <summary>
    /// "Luigi" e "Mario", con Mario nel gioco.
    /// </summary>
    private SaveManager TwoPlaythroughs()
    {
        SaveManager manager = folder.CreateManager("L");
        manager.CreateProfile("Luigi", true, TestFolder.Owner);
        TestFolder.Write(folder.SaveFile, "M");
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        manager.ActivateProfile("Profilo_Mario");
        return manager;
    }

    // Outer Wilds sul Deck: il primo posto è *.owprofile nella cartella, e la riga lo deve dire.
    [Fact]
    public void SaveFolderLineShowsThePatternOfTheFirstSpot()
    {
        TestFolder.Write(Path.Combine(folder.Game, "a.owprofile"), "x");
        var game = new GameInfo("steam", "753640", "Outer Wilds", "", "", "",
            [new SaveSpot(1, folder.Game, "*.owprofile", false, folder.Game, SaveSource.Launcher)],
            SupportState.Supported, Source: SaveSource.Launcher);

        var vm = new GameViewModel(new SaveManager(folder.Game, folder.Data, "", game), dialogs);

        Assert.Contains("*.owprofile", vm.SaveFolderText);
        Assert.Contains(folder.Game, vm.SaveFolderText);
    }

    [Fact]
    public void Refresh_PutsActivePlaythroughFirst()
    {
        var vm = new GameViewModel(TwoPlaythroughs(), dialogs);

        vm.Refresh();

        Assert.Equal(2, vm.Playthroughs.Count);
        Assert.Equal("Profilo_Mario", vm.Playthroughs[0].ProfileName);
        Assert.True(vm.Playthroughs[0].IsActive);
    }

    [Fact]
    public async Task Delete_AsksConfirmationAndDeletes()
    {
        SaveManager manager = TwoPlaythroughs();
        var vm = new GameViewModel(manager, dialogs);
        dialogs.ConfirmAnswer = true;
        vm.Refresh();

        await vm.DeleteAsync("Profilo_Luigi");

        Assert.Single(dialogs.Calls, c => c.StartsWith("Confirm"));
        Assert.DoesNotContain("Profilo_Luigi", manager.GetProfiles());
    }

    [Fact]
    public async Task Delete_Cancelled_KeepsPlaythrough()
    {
        SaveManager manager = TwoPlaythroughs();
        var vm = new GameViewModel(manager, dialogs);
        dialogs.ConfirmAnswer = false;
        vm.Refresh();

        await vm.DeleteAsync("Profilo_Luigi");

        Assert.Contains("Profilo_Luigi", manager.GetProfiles());
    }

    [Fact]
    public async Task AnAddedSpotIsListedAndCanBeRemoved()
    {
        string extra = Path.Combine(folder.Root, "extra");
        TestFolder.Write(Path.Combine(extra, "a.sav"), "x");

        var stored = new GameSpots(folder.Data);
        stored.Load();
        stored.Add(extra, "*.sav", extra, SaveSource.Chosen, recursive: false);
        stored.Save();

        var game = new GameInfo("steam", "123450", "Test", "", "", "",
            [
                new SaveSpot(1, folder.Game, "", true, folder.Game, SaveSource.Chosen),
                new SaveSpot(2, extra, "*.sav", false, extra, SaveSource.Chosen),
            ],
            SupportState.Supported);
        var vm = new GameViewModel(new SaveManager(folder.Game, folder.Data, "", game), dialogs);

        SpotLine line = Assert.Single(vm.SpotLines);
        Assert.True(line.Removable);
        Assert.Contains("*.sav", line.Text);

        dialogs.ConfirmAnswer = true;
        await line.RemoveCommand!.ExecuteAsync(null);

        Assert.Empty(vm.SpotLines);
        var after = new GameSpots(folder.Data);
        after.Load();
        Assert.DoesNotContain(after.Spots, spot => spot.Id == 2);
    }

    [Fact]
    public async Task AnImportedSpotInAGameWithPlaythroughsAsksCopyOrEmpty()
    {
        SaveManager manager = TwoPlaythroughs();
        string extra = Path.Combine(folder.Root, "extra");
        TestFolder.Write(Path.Combine(extra, "a.sav"), "x");
        var vm = new GameViewModel(manager, dialogs);
        dialogs.ConfirmAnswer = true;

        bool added = await vm.AddSpotWithChoiceAsync(new SaveSpot(0, extra, "", true, manager.PortableFolder(extra), SaveSource.Imported));

        Assert.True(added);
        Assert.Contains("Confirm: " + Strings.AddSpot, dialogs.Calls);
        Assert.Contains(vm.Manager.Game.Spots, spot => spot.Folder == extra && spot.Source == SaveSource.Imported);
    }

    [Fact]
    public async Task AnImportedSpotRefusedInTheChoiceIsNotAdded()
    {
        SaveManager manager = TwoPlaythroughs();
        string extra = Path.Combine(folder.Root, "extra");
        TestFolder.Write(Path.Combine(extra, "a.sav"), "x");
        var vm = new GameViewModel(manager, dialogs);
        dialogs.ConfirmAnswer = false;

        bool added = await vm.AddSpotWithChoiceAsync(new SaveSpot(0, extra, "", true, manager.PortableFolder(extra), SaveSource.Imported));

        Assert.False(added);
        Assert.DoesNotContain(vm.Manager.Game.Spots, spot => spot.Folder == extra);
    }

    // Senza la ricerca di altre cartelle dopo la partita: nei test non deve guardare fuori dalla cartella temporanea.
    private AppSettings NoLookAfter() => new(Path.Combine(folder.Root, "lss.json")) { LookAfterPlaying = false };

    // Sessione della modalità semplice: tempo e salvataggio li gestisce lei, il monitor non deve ripeterli.
    [Fact]
    public async Task SimpleSessionEnd_IsLeftToTheSimpleMode()
    {
        SaveManager manager = TwoPlaythroughs();
        var vm = new GameViewModel(manager, dialogs) { SimpleSessionActive = true, Settings = NoLookAfter() };
        TestFolder.Write(folder.SaveFile, "cambiato");
        DateTime end = DateTime.Now;

        await vm.OnGameState(new SessionEvent(SessionChange.Ended, "Profilo_Mario", end.AddMinutes(-10), end));

        Assert.Equal(TimeSpan.Zero, manager.GetInfo("Profilo_Mario").PlayTime);
        Assert.DoesNotContain(manager.GetBackupsOf("Profilo_Mario"), b => b.Kind == BackupKind.SaveOnExit);
    }

    [Fact]
    public async Task AdvancedSessionEnd_AddsTimeAndSavesOnce()
    {
        SaveManager manager = TwoPlaythroughs();
        var vm = new GameViewModel(manager, dialogs) { Settings = NoLookAfter() };
        TestFolder.Write(folder.SaveFile, "cambiato");
        DateTime end = DateTime.Now;

        await vm.OnGameState(new SessionEvent(SessionChange.Ended, "Profilo_Mario", end.AddMinutes(-10), end));

        Assert.Equal(TimeSpan.FromMinutes(10), manager.GetInfo("Profilo_Mario").PlayTime);
        Assert.Single(manager.GetBackupsOf("Profilo_Mario"), b => b.Kind == BackupKind.SaveOnExit);
    }
}
