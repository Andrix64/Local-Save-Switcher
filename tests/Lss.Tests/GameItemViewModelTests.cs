using Lss.App.ViewModels;

namespace Lss.Tests;

/// <summary>
/// GameItemViewModel: l'icona arriva in background e si mostra quando è pronta; senza
/// icona resta la lettera iniziale. Dati in TestFolder. Il passaggio alla UI
/// (ShowIcon) si prova a parte: la coda del dispatcher non si pompa nei test.
/// </summary>
public sealed class GameItemViewModelTests : IDisposable
{
    private readonly TestFolder folder = new();

    // Un PNG 1×1 valido, perché Avalonia lo deve poter leggere davvero.
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    public GameItemViewModelTests()
    {
        TestApp.Ensure();
    }

    public void Dispose() => folder.Dispose();

    [Fact]
    public void TheIconIsShownWhenItIsReady()
    {
        string png = Path.Combine(folder.Root, "icona.png");
        File.WriteAllBytes(png, Png);

        GameItemViewModel item = NewItem(new FakeProvider(), Path.Combine(folder.Root, "Gioco"));

        Assert.False(item.HasIcon);

        item.ShowIcon(png);

        Assert.True(item.HasIcon);
        Assert.NotNull(item.Icon);
    }

    private static GameItemViewModel NewItem(FakeProvider provider, string install)
    {
        var game = new GameInfo("fake", "1", "Gioco", "", "", install, [], SupportState.NoSaves, Source: SaveSource.None);
        var account = new GameAccount("fake", "x", "Fake", "", null);

        return new GameItemViewModel(game, account, provider);
    }
}
