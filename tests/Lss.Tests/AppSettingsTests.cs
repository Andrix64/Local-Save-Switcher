namespace Lss.Tests;

public sealed class AppSettingsTests : IDisposable
{
    private readonly TestFolder folder = new();

    private string JsonPath => Path.Combine(folder.Root, "lss.json");

    public void Dispose() => folder.Dispose();

    [Fact]
    public void RoundTrip_KeepsAccountAndGame()
    {
        var written = new AppSettings(JsonPath) { LastAccount = "111", LastGame = "870780" };
        written.Save();

        var read = new AppSettings(JsonPath);
        read.Load();

        Assert.Equal("111", read.LastAccount);
        Assert.Equal("870780", read.LastGame);
    }

    [Fact]
    public void MissingFile_GivesEmptyValues()
    {
        var settings = new AppSettings(JsonPath);
        settings.Load();

        Assert.Equal("", settings.LastAccount);
        Assert.Equal("", settings.LastGame);
    }

    [Fact]
    public void BrokenFile_GivesEmptyValuesWithoutException()
    {
        File.WriteAllBytes(JsonPath,[0, 1, 2, 255, 254, 13, 10, 0, 200, 201, 202]);

        var settings = new AppSettings(JsonPath);
        settings.Load();

        Assert.Equal("", settings.LastAccount);
        Assert.Equal("", settings.LastGame);
    }

    [Fact]
    public void ModeRoundTrip()
    {
        new AppSettings(JsonPath) { Mode = AppSettings.SimpleMode }.Save();

        var read = new AppSettings(JsonPath);
        read.Load();

        Assert.Equal(AppSettings.SimpleMode, read.Mode);
    }

    [Fact]
    public void ExistingFileWithoutModeIsAdvancedAndNotAsked()
    {
        TestFolder.Write(JsonPath, "UltimoAccount=111\n");

        var settings = new AppSettings(JsonPath);
        settings.Load();

        Assert.Equal(AppSettings.AdvancedMode, settings.Mode);
        Assert.False(settings.FirstStart);
    }

    [Fact]
    public void MissingFileAsksTheModeOnFirstStart()
    {
        var settings = new AppSettings(JsonPath);
        settings.Load();

        Assert.True(settings.FirstStart);
        Assert.Equal(AppSettings.AdvancedMode, settings.Mode);
    }
}
