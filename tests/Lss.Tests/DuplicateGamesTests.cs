namespace Lss.Tests;

/// <summary>
/// Lo stesso gioco (stessa cartella d'installazione) in due launcher: si mostra
/// solo quello del launcher che viene prima.
/// </summary>
public sealed class DuplicateGamesTests
{
    private static GameInfo Game(string provider, string id, string installPath) =>
        new(provider, id, id, "", "", installPath, [], SupportState.NoSaves);

    [Fact]
    public void TheSecondLauncherIsLeftOut()
    {
        var games = new[]
        {
            Game("epic", "Hades", Path.Combine("Giochi", "Hades")),
            Game("heroic", "legendary_Hades", Path.Combine("Giochi", "Hades")),
        };

        Assert.Equal([true, false], DuplicateGames.Keep(games));
    }

    [Fact]
    public void SameLauncherKeepsBothAndGamesWithoutFolderAreNotCompared()
    {
        var games = new[]
        {
            Game("heroic", "a", Path.Combine("Giochi", "A")),
            Game("heroic", "b", Path.Combine("Giochi", "A")),
            Game("manual", "c", ""),
            Game("manual", "d", ""),
        };

        Assert.Equal([true, true, true, true], DuplicateGames.Keep(games));
    }

    [Fact]
    public void FirstLauncherWinsWhateverTheOrder()
    {
        var games = new[]
        {
            Game("steam", "Gioco", Path.Combine("Giochi", "Gioco")),
            Game("gog", "1", Path.Combine("Giochi", "Gioco") + Path.DirectorySeparatorChar),
        };

        Assert.Equal([true, false], DuplicateGames.Keep(games));
    }
}
