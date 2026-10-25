namespace Lss.Tests;

/// <summary>
/// Avvio del gioco e di Steam, con la piattaforma finta.
/// </summary>
public sealed class PlatformTests : IDisposable
{
    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    [Fact]
    public void LaunchGameOpensSteamUri()
    {
        Result result = folder.CreateManager().LaunchGame();

        Assert.True(result.Success);
        Assert.Contains("steam://rungameid/870780", TestSetup.Fake.OpenedUris);
    }

    [Fact]
    public void LaunchGame_UsesTheGameAppId()
    {
        var game = new GameInfo("steam", "123450", "Test", "", "", "", folder.Game, SupportState.Supported);
        var manager = new SaveManager(folder.Game, folder.Data, "", game);

        Result result = manager.LaunchGame();

        Assert.True(result.Success);
        Assert.Contains("steam://rungameid/123450", TestSetup.Fake.OpenedUris);
        Assert.Contains("Test", result.Message);
    }
}
