namespace Lss.Tests;

/// <summary>
/// GameSpots: posti.ini nella cartella dati di un gioco.
/// </summary>
public sealed class GameSpotsTests : IDisposable
{
    private readonly TestFolder folder = new();

    private string Data => Path.Combine(folder.Root, "game-data");
    private string Posti => Path.Combine(Data, "posti.ini");

    public void Dispose() => folder.Dispose();

    [Fact]
    public void SpotsAndExtraRootsSurviveARestart()
    {
        string first = Path.Combine(folder.Root, "saves");
        string second = Path.Combine(folder.Root, "other");
        string extra = Path.Combine(folder.Root, "extra");

        var spots = new GameSpots(Data);
        spots.Load();
        spots.SetPrimary(first, SaveSource.Chosen);
        spots.Add(second, "*.sav", "{WinAppDataRoaming}/Foo", SaveSource.Learned, recursive: false);
        spots.AddExtraRoot(extra);
        spots.Save();

        var loaded = new GameSpots(Data);
        loaded.Load();

        Assert.Equal(2, loaded.Spots.Count);
        SaveSpot whole = loaded.Spots[0];
        Assert.Equal(1, whole.Id);
        Assert.Equal(first, whole.Folder);
        Assert.True(whole.IsWholeFolder);
        Assert.Equal(SaveSource.Chosen, whole.Source);

        SaveSpot pattern = loaded.Spots[1];
        Assert.Equal(2, pattern.Id);
        Assert.Equal(second, pattern.Folder);
        Assert.Equal("*.sav", pattern.Pattern);
        Assert.False(pattern.Recursive);
        Assert.Equal("{WinAppDataRoaming}/Foo", pattern.PortablePath);
        Assert.Equal(SaveSource.Learned, pattern.Source);
        Assert.Equal([extra], loaded.ExtraRoots);
    }

    [Fact]
    public void IdsAreNotReusedAfterRemove()
    {
        var spots = new GameSpots(Data);
        spots.Load();
        spots.Add(folder.Root, "", folder.Root, SaveSource.Chosen);
        spots.Add(Path.Combine(folder.Root, "b"), "*.sav", "b", SaveSource.Chosen);
        spots.Remove(2);
        spots.Save();

        var loaded = new GameSpots(Data);
        loaded.Load();

        SaveSpot added = loaded.Add(Path.Combine(folder.Root, "c"), "", "c", SaveSource.Chosen);

        Assert.Equal(4, added.Id);
        Assert.Equal([3, 4], loaded.Spots.Select(spot => spot.Id));
    }

    [Fact]
    public void LauncherIdsStayTheSameAfterARestart()
    {
        var spots = new GameSpots(Data);
        spots.Load();
        spots.Add(folder.Root, "", folder.Root, SaveSource.Chosen);
        int roaming = spots.LauncherId("{WinAppDataRoaming}/Foo");
        int install = spots.LauncherId("{gameinstall}/saves");
        spots.Save();

        var loaded = new GameSpots(Data);
        loaded.Load();

        Assert.Equal(roaming, loaded.LauncherId("{WinAppDataRoaming}/Foo"));
        Assert.Equal(install, loaded.LauncherId("{gameinstall}/saves"));
        Assert.NotEqual(roaming, install);
        Assert.Equal(5, loaded.LauncherId("{WinMyDocuments}/Game"));
        Assert.Single(loaded.Spots);
    }

    [Fact]
    public void PathWithPipeIsRefused()
    {
        var spots = new GameSpots(Data);
        spots.Load();

        Assert.Throws<ArgumentException>(() => spots.Add("a|b", "", "a|b", SaveSource.Chosen));
    }


    [Fact]
    public void RemovedExtraSpotKeepsTheNextId()
    {
        var spots = new GameSpots(Data);
        spots.Load();
        spots.Add(folder.Root, "", folder.Root, SaveSource.Chosen);
        spots.Add(Path.Combine(folder.Root, "b"), "*.sav", "b", SaveSource.Chosen);
        spots.Remove(2);
        spots.Save();

        Assert.Contains("Prossimo=4", File.ReadAllText(Posti));
    }
}
