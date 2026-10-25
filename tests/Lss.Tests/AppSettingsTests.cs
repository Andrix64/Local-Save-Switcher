namespace Lss.Tests;

public sealed class AppSettingsTests : IDisposable
{
    private readonly TestFolder folder = new();

    private string IniPath => Path.Combine(folder.Root, "lss.ini");

    public void Dispose() => folder.Dispose();

    [Fact]
    public void RoundTrip_KeepsAccountAndGame()
    {
        var written = new AppSettings(IniPath) { LastAccount = "111", LastGame = "870780" };
        written.Save();

        var read = new AppSettings(IniPath);
        read.Load();

        Assert.Equal("111", read.LastAccount);
        Assert.Equal("870780", read.LastGame);
    }

    [Fact]
    public void OldValuesWithoutProviderAreSteam()
    {
        Assert.True(AppSettings.Refers("111", "steam", "111"));
        Assert.True(AppSettings.Refers("steam/111", "steam", "111"));
        Assert.False(AppSettings.Refers("epic/111", "steam", "111"));
        Assert.False(AppSettings.Refers("", "steam", "111"));
    }

    [Fact]
    public void MissingFile_GivesEmptyValues()
    {
        var settings = new AppSettings(IniPath);
        settings.Load();

        Assert.Equal("", settings.LastAccount);
        Assert.Equal("", settings.LastGame);
    }

    [Fact]
    public void BrokenFile_GivesEmptyValuesWithoutException()
    {
        File.WriteAllBytes(IniPath,[0, 1, 2, 255, 254, 13, 10, 0, 200, 201, 202]);

        var settings = new AppSettings(IniPath);
        settings.Load();

        Assert.Equal("", settings.LastAccount);
        Assert.Equal("", settings.LastGame);
    }

    [Fact]
    public void ModeRoundTrip()
    {
        new AppSettings(IniPath) { Mode = AppSettings.SimpleMode }.Save();

        var read = new AppSettings(IniPath);
        read.Load();

        Assert.Equal(AppSettings.SimpleMode, read.Mode);
    }

    [Fact]
    public void ExistingFileWithoutModeIsAdvancedAndNotAsked()
    {
        TestFolder.Write(IniPath, "UltimoAccount=111\n");

        var settings = new AppSettings(IniPath);
        settings.Load();

        Assert.Equal(AppSettings.AdvancedMode, settings.Mode);
        Assert.False(settings.FirstStart);
    }

    [Fact]
    public void MissingFileAsksTheModeOnFirstStart()
    {
        var settings = new AppSettings(IniPath);
        settings.Load();

        Assert.True(settings.FirstStart);
        Assert.Equal(AppSettings.AdvancedMode, settings.Mode);
    }
}
