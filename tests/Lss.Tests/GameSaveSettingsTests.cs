namespace Lss.Tests;

public sealed class GameSaveSettingsTests : IDisposable
{
    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    private string Data => Path.Combine(folder.Root, "dati", "steam", "111", "123450");
    private string File => Path.Combine(Data, "posti.json");

    [Fact]
    public void RoundTripKeepsFolderAndOrigin()
    {
        string saves = Path.Combine(folder.Root, "Salvataggi");

        var saved = new GameSaveSettings(Data) { Folder = saves, Origin = SaveSource.Learned };
        saved.Save();

        var loaded = new GameSaveSettings(Data);
        loaded.Load();

        Assert.Equal(saves, loaded.Folder);
        Assert.Equal(SaveSource.Learned, loaded.Origin);
    }

    [Fact]
    public void ChosenIsTheDefaultOrigin()
    {
        var settings = new GameSaveSettings(Data) { Folder = folder.Root, Origin = SaveSource.Chosen };
        settings.Save();

        var loaded = new GameSaveSettings(Data);
        loaded.Load();

        Assert.Equal(SaveSource.Chosen, loaded.Origin);
    }

    [Fact]
    public void MissingFileMeansNoChoice()
    {
        var settings = new GameSaveSettings(Data);
        settings.Load();

        Assert.Equal("", settings.Folder);
        Assert.Equal(SaveSource.Chosen, settings.Origin);
    }

    [Fact]
    public void EmptyFolderDeletesTheFile()
    {
        new GameSaveSettings(Data) { Folder = folder.Root }.Save();
        Assert.True(System.IO.File.Exists(File));

        new GameSaveSettings(Data) { Folder = "" }.Save();

        Assert.False(System.IO.File.Exists(File));
    }

    [Fact]
    public void PathWithSpacesAndNonAsciiLettersSurvives()
    {
        string saves = Path.Combine(folder.Root, "Giochi di città", "Salvataggi è più");

        new GameSaveSettings(Data) { Folder = saves, Origin = SaveSource.Chosen }.Save();

        var loaded = new GameSaveSettings(Data);
        loaded.Load();

        Assert.Equal(saves, loaded.Folder);
    }
}
