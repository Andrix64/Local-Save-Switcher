namespace Lss.Tests;

/// <summary>
/// SteamProvider deve dare gli stessi account e giochi delle funzioni statiche
/// di SaveManager e SteamLibrary, con una cartella Steam finta.
/// </summary>
public sealed class SteamProviderTests : IDisposable
{
    private readonly TestFolder folder = new();
    private readonly FakePlatform platform = new();

    private string SteamRoot => Path.Combine(folder.Root, "Steam");
    private string UserData => Path.Combine(SteamRoot, "userdata");

    public SteamProviderTests()
    {
        platform.SteamRoots.Add(SteamRoot);
    }

    public void Dispose() => folder.Dispose();

    [Fact]
    public void AccountsWithSupportedGamesAreTheSameAsTheStaticSearch()
    {
        TestFolder.Write(Path.Combine(UserData, "111", "105600", "remote", "a.sav"), "x");

        var provider = new SteamProvider(platform);
        IReadOnlyList<GameAccount> accounts = provider.FindAccounts();

        Assert.Equal(SteamProvider.FindAccountsWithGames(UserData).Select(a => a.Folder), accounts.Select(a => a.Folder));
        Assert.Single(accounts);
        Assert.Equal("steam", accounts[0].Provider);
        Assert.Equal("111", accounts[0].Id);
    }

    [Fact]
    public void WithoutSupportedGamesAccountsComeFromTheAnySearch()
    {
        TestFolder.Write(Path.Combine(UserData, "222", "814380", "remotecache.vdf"), "cache");

        IReadOnlyList<GameAccount> accounts = new SteamProvider(platform).FindAccounts();

        Assert.Equal(SteamProvider.FindAccountsWithAnyGame(UserData).Select(a => a.Folder), accounts.Select(a => a.Folder));
        Assert.Single(accounts);
    }

    [Fact]
    public void NoSteamGivesNoAccounts()
    {
        platform.SteamRoots.Clear();

        Assert.Empty(new SteamProvider(platform).FindAccounts());
    }

    [Fact]
    public void GamesAreTheSameAsTheLibraryReadsThem()
    {
        TestFolder.Write(Path.Combine(UserData, "111", "105600", "remote", "a.sav"), "x");
        TestFolder.Write(Path.Combine(SteamRoot, "steamapps", "appmanifest_105600.acf"),
            "\"AppState\"\n{\n\t\"appid\"\t\t\"105600\"\n\t\"name\"\t\t\"Terraria\"\n\t\"installdir\"\t\t\"Terraria\"\n}\n");

        var provider = new SteamProvider(platform);
        GameAccount account = provider.FindAccounts().Single();

        IReadOnlyList<GameInfo> games = provider.FindGames(account);

        var expected = SteamLibrary.FindGames(SteamRoot, account.Folder);
        Assert.Equal(expected.Select(g => g.Id), games.Select(g => g.Id));
        Assert.All(games, g => Assert.Equal("steam", g.Provider));
        Assert.Equal("Terraria", games[0].Name);
    }

    [Fact]
    public void RunningAndLaunchComeFromThePlatform()
    {
        var provider = new SteamProvider(platform);
        var game = GameInfo.Control(folder.Game);

        platform.GameRunning = true;
        platform.StartTime = new DateTime(2026, 10, 25, 10, 0, 0);
        platform.SteamRunning = true;

        Assert.True(provider.CanDetectRunning(game));
        Assert.True(provider.IsGameRunning(game));
        Assert.Equal(platform.StartTime, provider.GameStartTime(game));
        Assert.True(provider.IsLauncherRunning());

        Result result = provider.Launch(game);

        Assert.True(result.Success);
        Assert.Contains("steam://rungameid/870780", platform.OpenedUris);
    }

    [Fact]
    public void CloudDisabledInSharedConfig()
    {
        string account = Path.Combine(UserData, "111");
        TestFolder.Write(Path.Combine(account, "7", "remote", "sharedconfig.vdf"),
            "\"UserLocalConfigStore\"\n{\n\t\"Software\"\n\t{\n\t\t\"apps\"\n\t\t{\n\t\t\t\"870780\"\n\t\t\t{\n\t\t\t\t\"cloudenabled\"\t\t\"0\"\n\t\t\t}\n\t\t}\n\t}\n}\n");

        var provider = new SteamProvider(platform);
        var accountInfo = new GameAccount("steam", "111", "", account, null);

        Assert.Equal(CloudState.Disabled, provider.GetCloudState(accountInfo, GameInfo.Control(folder.Game)));
    }

    [Fact]
    public void ProvidersHaveSteamFirst()
    {
        IReadOnlyList<IGameProvider> all = Providers.All(platform);

        Assert.Single(all);
        Assert.Equal("steam", all[0].Id);
        Assert.Equal("Steam", all[0].DisplayName);
    }

    [Fact]
    public void SaveRulesComeFromAppInfoReadByFindGames()
    {
        TestFolder.Write(Path.Combine(UserData, "111", "105600", "remote", "a.sav"), "x");
        Directory.CreateDirectory(Path.Combine(SteamRoot, "appcache"));
        File.WriteAllBytes(
            Path.Combine(SteamRoot, "appcache", "appinfo.vdf"),
            SteamAppInfoTests.AppInfo(true, SteamAppInfoTests.TwoApps()));

        var provider = new SteamProvider(platform);
        GameAccount account = provider.FindAccounts().Single();
        GameInfo game = provider.FindGames(account).Single(g => g.Id == "105600");

        Assert.Equal(2, provider.SaveRules(account, game).Count);
        Assert.Equal("WinAppDataLocal", provider.SaveRules(account, game)[0].Root);
        Assert.Single(provider.RootOverrides(account, game));
    }

    [Fact]
    public void SaveRulesAreEmptyBeforeFindGamesAndWithoutAppInfo()
    {
        TestFolder.Write(Path.Combine(UserData, "111", "105600", "remote", "a.sav"), "x");

        var provider = new SteamProvider(platform);
        GameAccount account = provider.FindAccounts().Single();
        GameInfo game = new("steam", "105600", "Test", "", "", "", "", SupportState.Supported);

        Assert.Empty(provider.SaveRules(account, game));
        Assert.Empty(provider.RootOverrides(account, game));

        provider.FindGames(account);

        Assert.Empty(provider.SaveRules(account, game));
    }
}
