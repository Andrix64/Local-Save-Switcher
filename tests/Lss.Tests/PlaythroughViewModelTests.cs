using Lss.App.ViewModels;

namespace Lss.Tests;

/// <summary>
/// Testi e stati di una scheda partita: attiva, non attiva, avvio spento.
/// </summary>
public sealed class PlaythroughViewModelTests : IDisposable
{
    private readonly TestFolder folder = new();

    public PlaythroughViewModelTests() => TestApp.Ensure();

    public void Dispose() => folder.Dispose();

    /// <summary>
    /// "Luigi" e "Mario", con Mario nel gioco.
    /// </summary>
    private SaveManager TwoPlaythroughs()
    {
        SaveManager manager = folder.CreateManager("L");
        manager.CreateProfile("Luigi", true, TestFolder.Owner);
        TestFolder.Write(folder.SaveFile, "M");
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        manager.ActivateProfile("Profilo_Mario");
        return manager;
    }

    private static PlaythroughViewModel Card(SaveManager manager, GameViewModel game, string profile) =>
        new(new CardData(profile, manager.State.ActiveProfile == profile, manager.GetInfo(profile), null, 0), game);

    [Fact]
    public void Inactive_ShowsPlayThisAndCanDelete()
    {
        SaveManager manager = TwoPlaythroughs();
        var game = new GameViewModel(manager, new FakeDialogService());
        PlaythroughViewModel card = Card(manager, game, "Profilo_Luigi");

        Assert.False(card.IsActive);
        Assert.Equal(Strings.PlayThisButton, card.PlayText);
        Assert.True(card.ShowPlay);
        Assert.True(card.CanDelete);
        Assert.Equal("Luigi", card.Name);
        Assert.Equal(card.Name, card.AccessibleName);
    }

    [Fact]
    public void Active_ShowsPlayAndCannotBeDeleted()
    {
        SaveManager manager = TwoPlaythroughs();
        var game = new GameViewModel(manager, new FakeDialogService());
        PlaythroughViewModel card = Card(manager, game, "Profilo_Mario");

        Assert.True(card.IsActive);
        Assert.Equal(Strings.PlayButton, card.PlayText);
        Assert.False(card.CanDelete);
        Assert.Equal("Mario" + Strings.ActiveSuffix, card.AccessibleName);
    }

    [Fact]
    public void Active_WithLaunchGameOff_HidesPlay()
    {
        SaveManager manager = TwoPlaythroughs();
        manager.State.LaunchGame = false;
        var game = new GameViewModel(manager, new FakeDialogService());

        Assert.False(Card(manager, game, "Profilo_Mario").ShowPlay);
        Assert.True(Card(manager, game, "Profilo_Luigi").ShowPlay);
    }

    [Fact]
    public void WhileGameRuns_CardCannotPlay()
    {
        SaveManager manager = TwoPlaythroughs();
        var game = new GameViewModel(manager, new FakeDialogService());
        PlaythroughViewModel card = Card(manager, game, "Profilo_Luigi");

        Assert.True(card.CanPlay);

        game.GameRunning = true;

        Assert.False(card.CanPlay);
    }
}
