using Lss.App.ViewModels;

namespace Lss.Tests;

/// <summary>
/// Cronologia: quando si può salvare un punto nuovo.
/// </summary>
public sealed class HistoryViewModelTests : IDisposable
{
    private readonly TestFolder folder = new();

    public HistoryViewModelTests() => TestApp.Ensure();

    public void Dispose() => folder.Dispose();

    private HistoryViewModel History(SaveManager manager) =>
        new(manager, new FakeDialogService(), () => Task.FromResult(true), () => Task.FromResult(false));

    [Fact]
    public void SavePoint_ValidName_CanSave()
    {
        SaveManager manager = folder.CreateManager("L");
        manager.CreateProfile("Luigi", true, TestFolder.Owner);
        manager.ActivateProfile("Profilo_Luigi");
        HistoryViewModel vm = History(manager);

        vm.SaveName = "Prima del boss";

        Assert.True(vm.CanSave);
        Assert.False(vm.SaveHintIsError);
    }

    [Fact]
    public void SavePoint_InvalidName_CannotSave()
    {
        SaveManager manager = folder.CreateManager("L");
        manager.CreateProfile("Luigi", true, TestFolder.Owner);
        manager.ActivateProfile("Profilo_Luigi");
        HistoryViewModel vm = History(manager);

        vm.SaveName = "a/b";

        Assert.False(vm.CanSave);
        Assert.True(vm.SaveHintIsError);
    }

    [Fact]
    public void SavePoint_WhileGameRuns_CannotSave()
    {
        SaveManager manager = folder.CreateManager("L");
        manager.CreateProfile("Luigi", true, TestFolder.Owner);
        manager.ActivateProfile("Profilo_Luigi");
        HistoryViewModel vm = History(manager);
        vm.SaveName = "Prima del boss";

        vm.GameRunning = true;

        Assert.False(vm.CanSave);
        Assert.True(vm.SaveHintIsWarning);
    }
}
