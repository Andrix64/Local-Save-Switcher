using Lss.App.ViewModels;

namespace Lss.Tests;

/// <summary>Il pulsante dello schermo intero: segue la finestra, ricorda lo stato e si ritira durante il gioco.</summary>
public sealed class FullScreenTests : IDisposable
{
    private readonly TestFolder folder = new();

    public FullScreenTests() => TestApp.Ensure();

    public void Dispose() => folder.Dispose();

    private static (FullScreenViewModel Model, FakeWindowControl Window) Create()
    {
        var window = new FakeWindowControl();
        return (new FullScreenViewModel { Window = window }, window);
    }

    [Fact]
    public void TheCommandTogglesFullScreenBothWays()
    {
        (FullScreenViewModel model, FakeWindowControl window) = Create();
        Assert.False(model.IsFullScreen);
        Assert.Equal(Strings.FullScreenOn, model.Tip);

        model.ToggleCommand.Execute(null);

        Assert.True(model.IsFullScreen);
        Assert.Equal(Strings.FullScreenOff, model.Tip);

        model.ToggleCommand.Execute(null);

        Assert.False(model.IsFullScreen);
        Assert.Equal(["EnterFullScreen", "LeaveFullScreen"], window.Calls);
    }

    [Fact]
    public void TheIconFollowsTheRealStateAlsoWhenTheSystemChangesIt()
    {
        (FullScreenViewModel model, FakeWindowControl window) = Create();

        window.SystemSets(true);
        Assert.True(model.IsFullScreen);

        window.SystemSets(false);
        Assert.False(model.IsFullScreen);
    }

    [Fact]
    public async Task InFullScreenTheWindowMinimizesWhileTheGameRunsAndComesBack()
    {
        (FullScreenViewModel model, FakeWindowControl window) = Create();
        model.Toggle();
        window.Calls.Clear();
        bool minimizedDuringGame = false;

        await model.WhileAwayAsync(() =>
        {
            minimizedDuringGame = window.IsMinimized;
            return Task.CompletedTask;
        });

        Assert.True(minimizedDuringGame);
        Assert.Equal(["Minimize", "Restore True"], window.Calls);
        Assert.True(model.IsFullScreen);
    }

    [Fact]
    public async Task MinimizingForTheGameIsNotRememberedAsLeavingFullScreen()
    {
        (FullScreenViewModel model, _) = Create();
        model.Toggle();
        var seen = new List<bool>();
        model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FullScreenViewModel.IsFullScreen))
                seen.Add(model.IsFullScreen);
        };

        await model.WhileAwayAsync(() => Task.CompletedTask);

        Assert.DoesNotContain(false, seen);
    }

    [Fact]
    public async Task NotInFullScreenTheWindowIsLeftAlone()
    {
        (FullScreenViewModel model, FakeWindowControl window) = Create();

        await model.WhileAwayAsync(() => Task.CompletedTask);

        Assert.Empty(window.Calls);
    }

    [Fact]
    public async Task TheWindowComesBackEvenIfTheGameFlowFails()
    {
        (FullScreenViewModel model, FakeWindowControl window) = Create();
        model.Toggle();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            model.WhileAwayAsync(() => throw new InvalidOperationException("x")));

        Assert.Contains("Restore True", window.Calls);
    }

    [Fact]
    public void LeavingTakesTheWindowOutOfFullScreen()
    {
        (FullScreenViewModel model, FakeWindowControl window) = Create();
        model.Toggle();

        model.Leave();

        Assert.False(model.IsFullScreen);
        Assert.False(window.IsFullScreen);
    }

    [Fact]
    public void FullScreenIsSavedAndReadBack()
    {
        var written = new AppSettings(Path.Combine(folder.Root, "lss.ini")) { Mode = AppSettings.SimpleMode, FullScreen = true };
        written.Save();

        var read = new AppSettings(Path.Combine(folder.Root, "lss.ini"));
        read.Load();

        Assert.True(read.FullScreen);
        Assert.False(new AppSettings(Path.Combine(folder.Root, "nuovo.ini")).FullScreen);
    }
}
