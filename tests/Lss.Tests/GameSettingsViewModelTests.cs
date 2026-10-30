using Lss.App.ViewModels;

namespace Lss.Tests;

/// <summary>
/// Impostazioni di un gioco: quelle dello stato del gioco, i file condivisi e le copie da tenere.
/// Quelle di LSS (esporta tutte, cercare dopo aver giocato) sono in LssSettingsViewModelTests.
/// </summary>
public sealed class GameSettingsViewModelTests : IDisposable
{
    private readonly TestFolder folder = new();
    private readonly FakeDialogService dialogs = new();

    public GameSettingsViewModelTests() => TestApp.Ensure();

    public void Dispose() => folder.Dispose();

    [Fact]
    public async Task TheGameSettingsAreSavedInTheGameState()
    {
        SaveManager manager = folder.CreateManager("L");
        var vm = new GameSettingsViewModel(manager, dialogs);
        Assert.True(vm.LaunchGame);

        vm.LaunchGame = false;
        vm.SaveOnExit = false;
        vm.KeepCopies = 7;

        Assert.True(await vm.TrySaveAsync());

        var read = new AppState(Path.Combine(folder.Data, "stato.json"));
        read.Load();
        Assert.False(read.LaunchGame);
        Assert.False(read.SaveOnGameExit);
        Assert.Equal(7, read.BackupsToKeep);
    }

    [Fact]
    public void TheTitleNamesTheGame()
    {
        var vm = new GameSettingsViewModel(folder.CreateManager("L"), dialogs);

        Assert.Equal(Loc.F(Strings.GameSettingsTitle, "Control"), vm.Title);
    }
}
