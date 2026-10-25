using Lss.App.ViewModels;

namespace Lss.Tests;

/// <summary>
/// Finestra principale con più launcher: intestazioni dei gruppi e account di
/// ogni gioco. Usa una cartella dei dati di prova, non quella vera.
/// </summary>
[Collection("AppData")]
public sealed class MainWindowViewModelTests : IDisposable
{
    private readonly TestFolder folder = new();
    private readonly string savedRoot = AppData.Root;

    public MainWindowViewModelTests()
    {
        AppData.Root = folder.Data;
        TestApp.Ensure();
    }

    public void Dispose()
    {
        AppData.Root = savedRoot;
        folder.Dispose();
    }

    private GameInfo Game(string provider, string id, string name, SupportState support = SupportState.Supported) =>
        new(provider, id, name, "", "", "", GameInfo.SpotsOf(Path.Combine(folder.Root, provider, id), SaveSource.Userdata), support);

    private MainWindowViewModel Create(params LauncherGames[] launchers) =>
        new(launchers, new AppSettings(Path.Combine(folder.Root, "lss.ini")), new FakeDialogService());

    [Fact]
    public void OneLauncherHasNoHeaders()
    {
        var provider = new FakeProvider { Id = "steam", DisplayName = "Alfa" };
        var account = new GameAccount("steam", "111", "Anna", folder.Root, null);

        MainWindowViewModel vm = Create(new LauncherGames(provider, account,
            [Game("steam", "1", "Uno"), Game("steam", "2", "Due")]));

        Assert.All(vm.Games, g => Assert.Equal("", g.LauncherHeader));
        Assert.True(vm.ShowSidebar);
    }

    [Fact]
    public void TwoLaunchersHaveAHeaderOnTheFirstGameOfEach()
    {
        var alfa = new FakeProvider { Id = "a", DisplayName = "Alfa" };
        var beta = new FakeProvider { Id = "b", DisplayName = "Beta" };
        var anna = new GameAccount("a", "1", "Anna", folder.Root, null);
        var bruno = new GameAccount("b", "2", "Bruno", folder.Root, null);

        MainWindowViewModel vm = Create(
            new LauncherGames(alfa, anna, [Game("a", "1", "Uno")]),
            new LauncherGames(beta, bruno, [Game("b", "2", "Due"), Game("b", "3", "Tre")]));

        Assert.Equal(new[] { "Alfa", "Beta", "" }, vm.Games.Select(g => g.LauncherHeader));
        Assert.Equal(bruno, vm.Games[1].Account);
        Assert.Equal("Anna  ·  Bruno", vm.AccountText);
    }

    [Fact]
    public void OtherGamesGetTheHeaderOfTheirLauncher()
    {
        var alfa = new FakeProvider { Id = "a", DisplayName = "Alfa" };
        var beta = new FakeProvider { Id = "b", DisplayName = "Beta" };
        var anna = new GameAccount("a", "1", "Anna", folder.Root, null);
        var bruno = new GameAccount("b", "2", "Bruno", folder.Root, null);

        MainWindowViewModel vm = Create(
            new LauncherGames(alfa, anna, [Game("a", "1", "Uno", SupportState.SavesElsewhere)]),
            new LauncherGames(beta, bruno, [Game("b", "2", "Due", SupportState.SavesElsewhere)]));

        Assert.Equal(new[] { "Alfa", "Beta" }, vm.OtherGames.Select(g => g.LauncherHeader));
    }
}
