namespace Lss.Tests;

/// <summary>
/// SaveConfig: la configurazione dei salvataggi scritta e letta (N9a).
/// </summary>
public sealed class SaveConfigTests
{
    private static SaveContext Context(string prefix = "") => new(
        new KnownFolders("/home/a", "", "", "", "", "", "", "", "/home/a/.local/share", "/home/a/.config", "a"),
        prefix,
        prefix != "",
        "",
        "123",
        IgnoreCase: false);

    private static GameInfo Game(params SaveSpot[] spots) =>
        new("steam", "632470", "Disco Elysium", "Disco Elysium", "", "", spots, SupportState.Supported);

    [Fact]
    public void RoundTripKeepsTheSpotsAndTheSystem()
    {
        SaveSpot[] spots =
        [
            new(1, "x", "", true, "{WinAppDataLocalLow}/ZAUM Studio/Disco Elysium/SaveGames", SaveSource.Launcher),
            new(2, "y", "*.sav", false, "{gameinstall}/saves", SaveSource.Chosen),
        ];
        var (game, skipped) = SaveConfig.FromGame(Game(spots), Context("/pfx"), "per Andrea");
        var file = SaveConfig.CurrentFile([game]);

        Result<SaveConfigFile> read = SaveConfig.Read(SaveConfig.Write(file));

        Assert.True(read.Success);
        Assert.Empty(skipped);
        SaveConfigGame back = Assert.Single(read.Value!.Games);
        Assert.Equal("632470", back.Id);
        Assert.Equal("Disco Elysium", back.Name);
        Assert.True(back.Proton);
        Assert.Equal("per Andrea", back.Notes);
        Assert.Equal(2, back.Spots.Count);
        Assert.Equal(new SaveConfigSpot("WinAppDataLocalLow", "ZAUM Studio/Disco Elysium/SaveGames", "", true), back.Spots[0]);
        Assert.Equal(new SaveConfigSpot("gameinstall", "saves", "*.sav", false), back.Spots[1]);
        Assert.Equal(file.Os, read.Value.Os);
    }

    [Fact]
    public void SpotOutsideEveryStandardRootIsSkippedAndListed()
    {
        SaveSpot[] spots =
        [
            new(1, "/home/a/.local/share/x", "", true, "{LinuxXdgDataHome}/x", SaveSource.Launcher),
            new(2, "D:/Giochi/Saves", "", true, "D:/Giochi/Saves", SaveSource.Chosen),
        ];

        var (game, skipped) = SaveConfig.FromGame(Game(spots), Context());

        Assert.Single(game.Spots);
        Assert.Equal(["D:/Giochi/Saves"], skipped);
    }

    [Fact]
    public void SteamIdsAreNotWrittenInTheFile()
    {
        SaveSpot[] spots =
        [
            new(1, "x", "", true, "{WinAppDataRoaming}/Foo/{64BitSteamID}/{Steam3AccountID}", SaveSource.Launcher),
        ];
        var (game, _) = SaveConfig.FromGame(Game(spots), Context());

        string json = SaveConfig.Write(SaveConfig.CurrentFile([game]));

        Assert.DoesNotContain("76561197960265851", json);
        Assert.DoesNotContain("\"123\"", json);
        Assert.Contains("{64BitSteamID}", json);
    }

    [Fact]
    public void ManualGameAndSeveralGamesInOneFile()
    {
        var manual = new GameInfo("manual", "", "Mio gioco", "Mio gioco", "", "", [], SupportState.NoSaves);
        var (first, _) = SaveConfig.FromGame(Game(new SaveSpot(1, "x", "", true, "{WinMyDocuments}/Game", SaveSource.Launcher)), Context());
        var (second, _) = SaveConfig.FromGame(manual, Context());

        Result<SaveConfigFile> read = SaveConfig.Read(SaveConfig.Write(SaveConfig.CurrentFile([first, second])));

        Assert.True(read.Success);
        Assert.Equal(2, read.Value!.Games.Count);
        Assert.Equal("", read.Value.Games[1].Id);
        Assert.Equal("Mio gioco", read.Value.Games[1].Name);
    }

    [Fact]
    public void FileNamesFormatAndVersionAreWritten()
    {
        string json = SaveConfig.Write(SaveConfig.CurrentFile([]));

        Assert.Contains("\"format\": \"lss-save-config\"", json);
        Assert.Contains("\"version\": 1", json);
        Assert.Contains("\"steamDeck\"", json);
    }
}
