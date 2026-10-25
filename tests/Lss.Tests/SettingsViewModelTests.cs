using Lss.App.ViewModels;

namespace Lss.Tests;

/// <summary>
/// Impostazioni del gioco: il pulsante "Esporta tutte" chiama la funzione data dalla
/// finestra principale (che sa di tutti i giochi).
/// </summary>
public sealed class SettingsViewModelTests : IDisposable
{
    private readonly TestFolder folder = new();
    private readonly FakeDialogService dialogs = new();

    public SettingsViewModelTests() => TestApp.Ensure();

    public void Dispose() => folder.Dispose();

    [Fact]
    public async Task ExportAllRunsTheExporter()
    {
        bool exported = false;
        var vm = new SettingsViewModel(folder.CreateManager("L"), dialogs, () =>
        {
            exported = true;
            return Task.CompletedTask;
        });

        await vm.ExportAllCommand.ExecuteAsync(null);

        Assert.True(exported);
    }

    [Fact]
    public async Task LookAfterPlayingIsOnByDefaultAndSavedInLssIni()
    {
        string ini = Path.Combine(folder.Root, "lss.ini");
        var settings = new AppSettings(ini);
        var vm = new SettingsViewModel(folder.CreateManager("L"), dialogs, settings: settings);
        Assert.True(vm.LookAfterPlaying);

        vm.LookAfterPlaying = false;
        Assert.True(await vm.TrySaveAsync());

        var read = new AppSettings(ini);
        read.Load();
        Assert.False(read.LookAfterPlaying);
    }
}
