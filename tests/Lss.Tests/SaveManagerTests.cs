namespace Lss.Tests;

public sealed class SaveManagerTests : IDisposable
{
    private readonly TestFolder folder = new();

    private static GameAccount Account => new("steam", "111", "Mario", "", null);

    public void Dispose() => folder.Dispose();

    [Fact]
    public void Open_UsesAccountAndGameFolder()
    {
        var game = new GameInfo("steam", "123450", "Test", "", "", "", folder.Game, SupportState.Supported);

        Result<SaveManager> result = SaveManager.Open(folder.Data, Account, game);

        Assert.True(result.Success);
        Assert.Equal(Path.Combine(folder.Data, "111", "123450", "Profili"), result.Value!.ProfilesPath);
        Assert.Equal(game.Id, result.Value.Game.Id);
    }

    [Fact]
    public void Open_RefusesUnsupportedGame()
    {
        var game = new GameInfo("steam", "123450", "Test", "", "", "", folder.Game, SupportState.SavesElsewhere);

        Result<SaveManager> result = SaveManager.Open(folder.Data, Account, game);

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains("Test", result.Message);
        Assert.False(Directory.Exists(Path.Combine(folder.Data, "111")));
    }
}
