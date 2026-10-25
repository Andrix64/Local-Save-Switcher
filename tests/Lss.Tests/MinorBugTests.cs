namespace Lss.Tests;

/// <summary>
/// Bug a priorità bassa trovati nella revisione della 0.9.0.
/// </summary>
public sealed class MinorBugTests : IDisposable
{
    private readonly TestFolder folder = new();

    private string Source => Path.Combine(folder.Root, "sorgente");
    private string Destination => Path.Combine(folder.Root, "destinazione");

    public void Dispose() => folder.Dispose();

    [Fact]
    public void FoldersWithFormerlyExcludedNamesAreCopied()
    {
        // Prima "Backup", "Profili" e "Profilo_*" venivano saltate a ogni livello,
        // e la verifica non se ne accorgeva.
        TestFolder.Write(Path.Combine(Source, "remote", "Backup", "a.sav"), "a");
        TestFolder.Write(Path.Combine(Source, "remote", "Profili", "b.sav"), "b");
        TestFolder.Write(Path.Combine(Source, "remote", "Profilo_X", "c.sav"), "c");

        FileOps.CopyFolderVerified(Source, Destination);

        Assert.Equal("a", File.ReadAllText(Path.Combine(Destination, "remote", "Backup", "a.sav")));
        Assert.Equal("b", File.ReadAllText(Path.Combine(Destination, "remote", "Profili", "b.sav")));
        Assert.Equal("c", File.ReadAllText(Path.Combine(Destination, "remote", "Profilo_X", "c.sav")));
    }

    [Fact]
    public void SteamCacheIsSkippedOnlyAtTopLevel()
    {
        TestFolder.Write(Path.Combine(Source, "remotecache.vdf"), "cache di Steam");
        TestFolder.Write(Path.Combine(Source, "remote", "remotecache.vdf"), "file del gioco");

        FileOps.CopyFolderVerified(Source, Destination);

        Assert.False(File.Exists(Path.Combine(Destination, "remotecache.vdf")));
        Assert.Equal("file del gioco", File.ReadAllText(Path.Combine(Destination, "remote", "remotecache.vdf")));

        // Un file con quel nome più in basso è un salvataggio come gli altri.
        File.WriteAllText(Path.Combine(Destination, "remote", "remotecache.vdf"), "diverso");
        Assert.False(FileOps.FoldersAreEqual(Source, Destination));
    }

    [Fact]
    public void ReplaceFolder_ReplacesFoldersWithFormerlyExcludedNames()
    {
        TestFolder.Write(Path.Combine(Source, "remote", "a.sav"), "nuovo");
        TestFolder.Write(Path.Combine(Destination, "remote", "Backup", "vecchio.sav"), "da togliere");
        TestFolder.Write(Path.Combine(Destination, "remotecache.vdf"), "cache");

        FileOps.ReplaceFolder(Source, Destination);

        Assert.True(FileOps.FoldersAreEqual(Source, Destination));
        Assert.False(Directory.Exists(Path.Combine(Destination, "remote", "Backup")));
        Assert.Equal("cache", File.ReadAllText(Path.Combine(Destination, "remotecache.vdf")));
    }

    [Fact]
    public void HasNoSaveFiles_CountsNestedFileNamedLikeSteamCache()
    {
        TestFolder.Write(Path.Combine(Source, "remotecache.vdf"), "cache");
        Assert.True(FileOps.HasNoSaveFiles(Source));

        TestFolder.Write(Path.Combine(Source, "remote", "remotecache.vdf"), "x");
        Assert.False(FileOps.HasNoSaveFiles(Source));
    }

    [Fact]
    public void IgnoredProfileFoldersAreListed()
    {
        SaveManager manager = folder.CreateManager();
        manager.CreateProfile("Mario", false, TestFolder.Owner);
        Directory.CreateDirectory(Path.Combine(manager.ProfilesPath, "Profilo_Mio.Nome"));
        Directory.CreateDirectory(Path.Combine(manager.ProfilesPath, "Altro"));

        Assert.Equal(new[] { "Altro", "Profilo_Mio.Nome" }, manager.GetIgnoredProfileFolders());
        Assert.Equal(new[] { "Profilo_Mario" }, manager.GetProfiles());
    }

    [Fact]
    public void NoIgnoredFoldersWhenAllAreValid()
    {
        SaveManager manager = folder.CreateManager();
        manager.CreateProfile("Mario", false, TestFolder.Owner);

        Assert.Empty(manager.GetIgnoredProfileFolders());
    }
}
