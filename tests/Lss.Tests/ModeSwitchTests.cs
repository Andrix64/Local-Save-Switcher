using Lss.App.ViewModels;

namespace Lss.Tests;

/// <summary>
/// L'interruttore Semplice | Avanzata: passa da una modalità all'altra (dalla semplice all'avanzata serve
/// un admin), e resta spento mentre si gioca.
/// </summary>
[Collection("AppData")]
public sealed class ModeSwitchTests : IDisposable
{
    private const string Admin = "a1b2c3d4";
    private const string User = "b2c3d4e5";

    private readonly TestFolder folder = new();
    private readonly string savedRoot = AppData.Root;
    private readonly FakeDialogService dialogs = new();

    public ModeSwitchTests()
    {
        AppData.Root = folder.Data;
        TestApp.Ensure();
    }

    public void Dispose()
    {
        AppData.Root = savedRoot;
        folder.Dispose();
    }

    private People People(bool adminHasPin = false)
    {
        TestFolder.WritePeople(folder.Data,
            $"Ordine={Admin},{User}\n" +
            $"{Admin}.Nome=Andrea\n{Admin}.Ruolo=admin\n" +
            $"{User}.Nome=Clara\n{User}.Ruolo=utente\n");

        var people = new People(folder.Data);
        people.Load();

        if (adminHasPin)
            people.SetPin(Admin, "1234");

        return people;
    }

    private (MainWindowViewModel Model, AppSettings Settings) Create(string mode, People people)
    {
        var settings = new AppSettings(Path.Combine(folder.Root, "lss.json")) { Mode = mode };
        var provider = new FakeProvider { Id = "steam", DisplayName = "Steam" };
        var account = new GameAccount("steam", "111", "Anna", folder.Root, null);
        var game = new GameInfo("steam", "1", "Uno", "", "", "",
            GameInfo.SpotsOf(Path.Combine(folder.Root, "steam", "1"), SaveSource.Userdata), SupportState.Supported);

        return (new MainWindowViewModel([new LauncherGames(provider, account, [game])], settings, dialogs, people), settings);
    }

    [Fact]
    public async Task AdvancedToSimpleNeedsNoOne()
    {
        (MainWindowViewModel model, AppSettings settings) = Create(AppSettings.AdvancedMode, People(adminHasPin: true));
        Assert.True(model.Mode.IsAdvanced);

        await model.Mode.ShowSimpleCommand.ExecuteAsync(null);

        Assert.True(model.IsSimple);
        Assert.True(model.Mode.IsSimple);
        Assert.Equal(AppSettings.SimpleMode, settings.Mode);
        Assert.DoesNotContain("Admin", dialogs.Calls);
    }

    [Fact]
    public async Task SimpleToAdvancedWithAnAdminWorks()
    {
        People people = People(adminHasPin: true);
        (MainWindowViewModel model, AppSettings settings) = Create(AppSettings.SimpleMode, people);
        dialogs.ChosenAdmin = people.Find(Admin);

        await model.Mode.ShowAdvancedCommand.ExecuteAsync(null);

        Assert.Contains("Admin", dialogs.Calls);
        Assert.False(model.IsSimple);
        Assert.True(model.Mode.IsAdvanced);
        Assert.Equal(AppSettings.AdvancedMode, settings.Mode);
    }

    [Fact]
    public async Task ARefusedSwitchLeavesTheModeAsItWas()
    {
        (MainWindowViewModel model, AppSettings settings) = Create(AppSettings.SimpleMode, People(adminHasPin: true));
        dialogs.ChosenAdmin = null;

        await model.Mode.ShowAdvancedCommand.ExecuteAsync(null);

        Assert.True(model.IsSimple);
        Assert.True(model.Mode.IsSimple);
        Assert.Equal(AppSettings.SimpleMode, settings.Mode);
    }

    [Fact]
    public async Task ChoosingTheCurrentModeDoesNothing()
    {
        (MainWindowViewModel model, _) = Create(AppSettings.SimpleMode, People());

        await model.Mode.ShowSimpleCommand.ExecuteAsync(null);

        Assert.True(model.IsSimple);
        Assert.Empty(dialogs.Calls);
    }

    [Fact]
    public void TheSimpleViewSharesTheSwitch()
    {
        (MainWindowViewModel model, _) = Create(AppSettings.SimpleMode, People());

        Assert.Same(model.Mode, model.Simple!.Mode);
    }

    [Fact]
    public void TheSwitchIsOffWhileAGameIsPlayedFromTheSimpleMode()
    {
        People people = People();
        (MainWindowViewModel model, _) = Create(AppSettings.SimpleMode, people);
        var changed = new List<string?>();
        model.Mode.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        Assert.True(model.Mode.Enabled);

        GameItemViewModel item = model.Games[0];
        var session = new PlaySession(folder.CreateManager(), item, people.Find(User)!, DateTime.Now, false, false);
        _ = model.Simple!.BeginPlaying(session);

        Assert.False(model.Mode.Enabled);
        Assert.False(model.Mode.ShowAdvancedCommand.CanExecute(null));
        Assert.False(model.Mode.ShowSimpleCommand.CanExecute(null));
        Assert.Contains(nameof(ModeSwitchViewModel.Enabled), changed);

        model.Simple.EndPlaying();

        Assert.True(model.Mode.Enabled);
        Assert.True(model.Mode.ShowAdvancedCommand.CanExecute(null));
    }

    [Fact]
    public async Task SwitchingToAdvancedLeavesFullScreenAndForgetsIt()
    {
        People people = People();
        (MainWindowViewModel model, AppSettings settings) = Create(AppSettings.SimpleMode, people);
        var window = new FakeWindowControl();
        model.AttachWindow(window);
        model.FullScreen.Toggle();
        Assert.True(settings.FullScreen);

        await model.Mode.ShowAdvancedCommand.ExecuteAsync(null);

        Assert.False(window.IsFullScreen);
        Assert.False(model.FullScreen.IsFullScreen);
        Assert.False(settings.FullScreen);
        Assert.False(model.IsSimple);
    }

    [Fact]
    public void FullScreenIsRememberedOnlyInTheSimpleMode()
    {
        People people = People();
        (MainWindowViewModel model, AppSettings settings) = Create(AppSettings.AdvancedMode, people);
        model.AttachWindow(new FakeWindowControl());

        model.FullScreen.Toggle();

        Assert.True(model.FullScreen.IsFullScreen);
        Assert.False(settings.FullScreen);
    }
}
