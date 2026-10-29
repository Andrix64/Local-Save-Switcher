using Lss.App.ViewModels;

namespace Lss.Tests;

public sealed class CloudConsentTests : IDisposable
{
    private readonly TestFolder folder = new();

    public CloudConsentTests() => TestApp.Ensure();

    public void Dispose() => folder.Dispose();

    private string StateFile => Path.Combine(folder.Data, "stato.json");

    [Fact]
    public void TheConsentIsWrittenAndReadBack()
    {
        SaveManager manager = folder.CreateManager("A1");

        Assert.True(manager.SetCloudAllowedInSimple(true).Success);

        var state = new AppState(StateFile);
        state.Load();
        Assert.True(state.CloudAllowedInSimple);
        Assert.Contains("\"CloudConsentito\": true", File.ReadAllText(StateFile));

        Assert.True(manager.SetCloudAllowedInSimple(false).Success);

        state.Load();
        Assert.False(state.CloudAllowedInSimple);
    }

    [Fact]
    public void AllowAndUndoFromTheGame()
    {
        SaveManager manager = folder.CreateManager("A1");
        var game = new GameViewModel(manager, new FakeDialogService());

        game.AllowCloudInSimpleCommand.Execute(null);
        Assert.True(game.CloudAllowedInSimple);

        game.UndoAllowCloudInSimpleCommand.Execute(null);
        Assert.False(game.CloudAllowedInSimple);
    }

    [Fact]
    public void ANewGameIsNotAllowedInSimpleMode()
    {
        SaveManager manager = folder.CreateManager("A1");

        var state = new AppState(StateFile);
        state.Load();
        Assert.False(state.CloudAllowedInSimple);
        Assert.False(new GameViewModel(manager, new FakeDialogService()).CloudAllowedInSimple);
    }
}
