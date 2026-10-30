using Lss.App.ViewModels;

namespace Lss.Tests;

/// <summary>
/// Anteprima della configurazione dei salvataggi (N9c): le note finiscono nel file,
/// Annulla non scrive nulla. Dialoghi finti.
/// </summary>
public sealed class ExportConfigViewModelTests : IDisposable
{
    private readonly TestFolder folder = new();
    private readonly FakeDialogService dialogs = new();

    public ExportConfigViewModelTests() => TestApp.Ensure();

    public void Dispose() => folder.Dispose();

    [Fact]
    public async Task SaveWritesTheNotesWithTheSpots()
    {
        string target = Path.Combine(folder.Root, "gioco.lss-config.json");
        dialogs.ConfigToSave = target;
        var vm = new ExportConfigViewModel(folder.CreateManager("L"), dialogs) { Notes = "  per Andrea  " };
        bool? finished = null;
        vm.Finished += saved => finished = saved;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(finished);
        Result<SaveConfigFile> read = SaveConfig.Read(File.ReadAllText(target));
        Assert.True(read.Success);
        SaveConfigGame game = Assert.Single(read.Value!.Games);
        Assert.Equal("per Andrea", game.Notes);
    }

    [Fact]
    public void CancelWritesNothing()
    {
        string target = Path.Combine(folder.Root, "gioco.lss-config.json");
        dialogs.ConfigToSave = target;
        var vm = new ExportConfigViewModel(folder.CreateManager("L"), dialogs) { Notes = "non serve" };
        bool? finished = null;
        vm.Finished += saved => finished = saved;

        vm.CancelCommand.Execute(null);

        Assert.False(finished);
        Assert.False(File.Exists(target));
        Assert.DoesNotContain(dialogs.Calls, call => call.StartsWith("SaveConfig"));
    }

    [Fact]
    public async Task ChoosingNoFileWritesNothing()
    {
        dialogs.ConfigToSave = null;
        var vm = new ExportConfigViewModel(folder.CreateManager("L"), dialogs);
        bool? finished = null;
        vm.Finished += saved => finished = saved;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Null(finished);
        Assert.Contains(dialogs.Calls, call => call.StartsWith("SaveConfig"));
    }
}
