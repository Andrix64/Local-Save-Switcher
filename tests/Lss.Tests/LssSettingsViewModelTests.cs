using Lss.App.ViewModels;

namespace Lss.Tests;

/// <summary>
/// Impostazioni di LSS: il pulsante "Esporta tutte" chiama la funzione data dalla finestra principale
/// (che sa di tutti i giochi); cercare dopo aver giocato si salva in lss.json.
/// </summary>
public sealed class LssSettingsViewModelTests : IDisposable
{
    private readonly TestFolder folder = new();
    private readonly FakeDialogService dialogs = new();

    public LssSettingsViewModelTests() => TestApp.Ensure();

    public void Dispose() => folder.Dispose();

    [Fact]
    public async Task PlayersOpensTheWindowOfPlayers()
    {
        bool opened = false;
        var vm = new LssSettingsViewModel(new AppSettings(Path.Combine(folder.Root, "lss.json")), dialogs,
            openPlayers: () =>
            {
                opened = true;
                return Task.CompletedTask;
            });

        await vm.OpenPlayersCommand.ExecuteAsync(null);

        Assert.True(opened);
    }

    [Fact]
    public void LookAfterPlayingIsOnByDefaultAndSavedAtOnceInLssIni()
    {
        string ini = Path.Combine(folder.Root, "lss.json");
        var settings = new AppSettings(ini);
        var vm = new LssSettingsViewModel(settings, dialogs);
        Assert.True(vm.LookAfterPlaying);

        vm.LookAfterPlaying = false;

        var read = new AppSettings(ini);
        read.Load();
        Assert.False(read.LookAfterPlaying);
    }
}
