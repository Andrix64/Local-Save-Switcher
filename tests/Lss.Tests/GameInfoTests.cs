namespace Lss.Tests;

/// <summary>
/// GameInfo con i posti: SaveFolder è la cartella del primo posto.
/// </summary>
public sealed class GameInfoTests
{
    [Fact]
    public void NoSpotMeansNoSaveFolder()
    {
        var game = new GameInfo("steam", "123450", "Test", "", "", "", [], SupportState.NotFound);

        Assert.Equal("", game.SaveFolder);
        Assert.Empty(game.Spots);
        Assert.Empty(GameInfo.SpotsOf("", SaveSource.None));
    }

    [Fact]
    public void OneSpotIsTheFirstFolder()
    {
        string folder = Path.Combine("C:", "saves", "123450");
        var game = new GameInfo("steam", "123450", "Test", "", "", "", GameInfo.SpotsOf(folder, SaveSource.Userdata),
            SupportState.Supported);

        Assert.Equal(folder, game.SaveFolder);
        SaveSpot spot = Assert.Single(game.Spots);
        Assert.Equal(1, spot.Id);
        Assert.True(spot.IsWholeFolder);
        Assert.Equal(SaveSource.Userdata, spot.Source);
    }
}
