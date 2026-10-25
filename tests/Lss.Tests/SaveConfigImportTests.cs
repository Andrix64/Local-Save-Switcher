namespace Lss.Tests;

/// <summary>
/// SaveConfig.Read (controlli del file non fidato), SaveConfigImport.Plan e ApplyAsync (N9b).
/// </summary>
public sealed class SaveConfigImportTests : IDisposable
{
    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    private string Install => Path.Combine(folder.Root, "lib", "steamapps", "common", "Game");
    private string Lss => Path.Combine(folder.Root, "lssdata");

    private SaveContext Context(string prefix = "") => new(
        new KnownFolders(Path.Combine(folder.Root, "home"), "", "", "", "", "", "", "", "", "", "andrix"),
        prefix,
        prefix != "",
        Install,
        "123",
        IgnoreCase: false);

    private static string Json(string spots, string extra = "") =>
        "{\"format\":\"lss-save-config\",\"version\":1,\"os\":\"linux\"," + extra +
        "\"games\":[{\"provider\":\"steam\",\"id\":\"632470\",\"name\":\"Disco\",\"installDir\":\"Disco\"," +
        "\"proton\":false,\"spots\":[" + spots + "],\"notes\":\"\"}]}";

    private static string Spot(string root, string path, string pattern = "", bool recursive = true) =>
        "{\"root\":\"" + root + "\",\"path\":\"" + path + "\",\"pattern\":\"" + pattern + "\",\"recursive\":" +
        (recursive ? "true" : "false") + "}";

    private static string Problem(string json)
    {
        Result<SaveConfigFile> result = SaveConfig.Read(json);
        Assert.False(result.Success);
        return result.Message;
    }

    [Fact]
    public void ValidFileIsAccepted()
    {
        Assert.True(SaveConfig.Read(Json(Spot("gameinstall", "saves", "*.sav", false))).Success);
    }

    [Fact]
    public void TooBigFileIsRefused()
    {
        string json = Json(Spot("gameinstall", "saves"), "\"pad\":\"" + new string('x', 1_100_000) + "\",");

        Assert.Contains("1 MB", Problem(json));
    }

    [Fact]
    public void OtherFormatIsRefused()
    {
        string json = Json(Spot("gameinstall", "saves")).Replace("lss-save-config", "altro");

        Assert.Contains("formato", Problem(json));
    }

    [Fact]
    public void NewerVersionIsRefused()
    {
        string json = Json(Spot("gameinstall", "saves")).Replace("\"version\":1", "\"version\":2");

        Assert.Equal(Strings.ConfigTooNew, Problem(json));
    }

    [Fact]
    public void TooManyGamesIsRefused()
    {
        string game = "{\"provider\":\"steam\",\"id\":\"1\",\"name\":\"A\",\"installDir\":\"\",\"proton\":false,\"spots\":[],\"notes\":\"\"}";
        string json = "{\"format\":\"lss-save-config\",\"version\":1,\"games\":[" + string.Join(",", Enumerable.Repeat(game, 201)) + "]}";

        Assert.Contains("giochi", Problem(json));
    }

    [Fact]
    public void TooManySpotsIsRefused()
    {
        string spots = string.Join(",", Enumerable.Repeat(Spot("gameinstall", "a"), 9));

        Assert.Contains("posti", Problem(Json(spots)));
    }

    [Theory]
    [InlineData("NotARoot", "saves")]
    [InlineData("gameinstall", "/etc")]
    [InlineData("gameinstall", "D:/Giochi")]
    [InlineData("gameinstall", "../x")]
    [InlineData("gameinstall", "a\\\\b")]
    [InlineData("gameinstall", "*.sav")]
    [InlineData("gameinstall", "{Other}")]
    public void UnsafeSpotIsRefused(string root, string path)
    {
        Problem(Json(Spot(root, path)));
    }

    [Fact]
    public void SteamIdTokensAreAllowed()
    {
        Assert.True(SaveConfig.Read(Json(Spot("WinAppDataRoaming", "Foo/{64BitSteamID}/{Steam3AccountID}"))).Success);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("*.exe")]
    [InlineData("a/b.sav")]
    [InlineData("??")]
    public void UnsafePatternIsRefused(string pattern)
    {
        Problem(Json(Spot("gameinstall", "saves", pattern)));
    }

    [Fact]
    public void UnknownPropertiesAreIgnored()
    {
        string json = Json(Spot("gameinstall", "saves"), "\"futuro\":{\"x\":1},");

        Assert.True(SaveConfig.Read(json).Success);
    }

    private static SaveConfigFile FileWith(params SaveConfigSpot[] spots) => new("linux", "Test", false, "LSS", "2026-10-25",
        [new SaveConfigGame("steam", "632470", "Disco", "Disco", false, spots, "")]);

    [Fact]
    public void GameIsMatchedByIdAndSpotsAreResolved()
    {
        Directory.CreateDirectory(Path.Combine(Install, "saves"));
        TestFolder.Write(Path.Combine(Install, "saves", "a.sav"), "x");
        var game = new GameInfo("steam", "632470", "Disco Elysium", "Disco", "", Install, [], SupportState.NoSaves);

        IReadOnlyList<ConfigGamePlan> plans = SaveConfigImport.Plan(
            FileWith(new SaveConfigSpot("gameinstall", "saves", "", true), new SaveConfigSpot("gameinstall", "later", "", true)),
            [game], _ => Context(), _ => [], Lss);

        ConfigGamePlan plan = Assert.Single(plans);
        Assert.Equal(game, plan.Game);
        Assert.Equal(ConfigSpotState.Found, plan.Spots[0].State);
        Assert.Equal(ConfigSpotState.NotYet, plan.Spots[1].State);
    }

    [Fact]
    public void GameIsMatchedByNameWhenTheIdIsDifferent()
    {
        var game = new GameInfo("steam", "999", "Disco Elysium", "Disco", "", Install, [], SupportState.NoSaves);

        ConfigGamePlan plan = Assert.Single(SaveConfigImport.Plan(FileWith(), [game], _ => Context(), _ => [], Lss));

        Assert.Equal(game, plan.Game);
    }

    [Fact]
    public void GameNotHereIsListed()
    {
        ConfigGamePlan plan = Assert.Single(SaveConfigImport.Plan(FileWith(), [], _ => Context(), _ => [], Lss));

        Assert.Null(plan.Game);
    }

    [Fact]
    public void WindowsFileOnAGameInAPrefixIsFlagged()
    {
        var game = new GameInfo("steam", "632470", "Disco", "Disco", "", Install, [], SupportState.NoSaves);
        SaveConfigFile file = FileWith(new SaveConfigSpot("gameinstall", "saves", "", true)) with { Os = "windows" };

        ConfigGamePlan plan = Assert.Single(SaveConfigImport.Plan(file, [game],
            _ => Context(Path.Combine(folder.Root, "pfx")), _ => [], Lss));

        Assert.True(plan.WindowsInPrefix);
    }

    [Fact]
    public void RootWithNoValueHereIsNotForThisSystem()
    {
        var game = new GameInfo("steam", "632470", "Disco", "Disco", "", Install, [], SupportState.NoSaves);

        ConfigGamePlan plan = Assert.Single(SaveConfigImport.Plan(
            FileWith(new SaveConfigSpot(OperatingSystem.IsLinux() ? "WinAppDataLocal" : "LinuxHome", "saves", "", true)), [game], _ => Context(), _ => [], Lss));

        Assert.Equal(ConfigSpotState.NotForThisSystem, Assert.Single(plan.Spots).State);
    }

    [Fact]
    public void FolderThatIsTheGameFolderIsRefused()
    {
        Directory.CreateDirectory(Install);
        var game = new GameInfo("steam", "632470", "Disco", "Disco", "", Install, [], SupportState.NoSaves);

        ConfigGamePlan plan = Assert.Single(SaveConfigImport.Plan(
            FileWith(new SaveConfigSpot("gameinstall", "", "", true)), [game], _ => Context(), _ => [], Lss));

        Assert.Equal(ConfigSpotState.Refused, Assert.Single(plan.Spots).State);
    }

    [Fact]
    public async Task ApplyWritesTheSpotsAsImported()
    {
        Directory.CreateDirectory(Path.Combine(Install, "saves"));
        var game = new GameInfo("steam", "632470", "Disco", "Disco", "", Install, [], SupportState.NoSaves);
        ConfigGamePlan plan = Assert.Single(SaveConfigImport.Plan(
            FileWith(new SaveConfigSpot("gameinstall", "saves", "", true), new SaveConfigSpot("gameinstall", "later", "", true)),
            [game], _ => Context(), _ => [], Lss));
        string data = Path.Combine(folder.Root, "gamedata");

        // Nessuna scrittura finché non si chiama ApplyAsync (l'annullo non lo chiama).
        Assert.False(File.Exists(Path.Combine(data, "posti.ini")));

        int added = await SaveConfigImport.ApplyAsync(plan, data, hasPlaythroughs: false, addWithPlaythroughs: null);

        Assert.Equal(2, added);
        Assert.Contains("importata", File.ReadAllText(Path.Combine(data, "posti.ini")));
        var stored = new GameSpots(data);
        stored.Load();
        Assert.All(stored.Spots, spot => Assert.Equal(SaveSource.Imported, spot.Source));
    }
}
