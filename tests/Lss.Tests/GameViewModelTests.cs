
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
        manager.CreateProfile("Luigi", true);
        TestFolder.Write(folder.SaveFile, "M");
        manager.CreateProfile("Mario", true);
        manager.ActivateProfile("Profilo_Mario");
        return manager;
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
    public async Task Import_UsesPickedZip()
    {
        SaveManager manager = TwoPlaythroughs();
        string zip = Path.Combine(folder.Root, "luigi.zip");
        Assert.True(manager.ExportProfile("Profilo_Luigi", zip).Success);

        dialogs.ZipToOpen = zip;
        var vm = new GameViewModel(manager, dialogs);
        vm.Refresh();

        await vm.ImportAsync();

        Assert.Contains("Open", dialogs.Calls);
        Assert.Equal(3, vm.Playthroughs.Count);
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
}
