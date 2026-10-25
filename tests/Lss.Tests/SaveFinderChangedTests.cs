namespace Lss.Tests;

/// <summary>
/// SaveFinder.FindChanged: i file scritti dopo l'avvio del gioco (M2). Solo
/// cartelle dentro TestFolder; le date dei file si impostano dal test.
/// </summary>
public sealed class SaveFinderChangedTests : IDisposable
{
    private readonly TestFolder folder = new();
    // Nel futuro: su Linux la data di creazione è quella del cambio di stato, cioè adesso,
    // e così resta sempre prima di since. Le date dei file si impostano dal test.
    private readonly DateTime since = DateTime.UtcNow.AddDays(1);

    public void Dispose() => folder.Dispose();

    private string Root => Path.Combine(folder.Root, "game");

    private string Write(string relative, DateTime written)
    {
        string path = Path.Combine(Root, relative);
        TestFolder.Write(path, "x");
        File.SetLastWriteTimeUtc(path, written);
        return path;
    }

    private IReadOnlyList<ChangedFile> Find(int depth = 8, IReadOnlyList<string>? skip = null,
        CancellationToken cancel = default) =>
        SaveFinder.FindChanged([new SearchRoot(Root, depth, true)], since, skip ?? [], TimeSpan.FromMinutes(1),
            cancel, out _);

    [Fact]
    public void FileWrittenAfterTheStartIsFoundAndOlderIsNot()
    {
        string newer = Write("new.sav", since.AddSeconds(10));
        Write("old.sav", since.AddHours(-1));

        ChangedFile found = Assert.Single(Find());

        Assert.Equal(newer, found.Path);
        Assert.Equal(since.AddSeconds(10), found.WrittenUtc);
    }

    [Fact]
    public void FileCreatedAfterTheStartIsFoundEvenIfWrittenBefore()
    {
        // Su Linux la data di creazione non si imposta: il caso si prova solo su Windows.
        if (!OperatingSystem.IsWindows()) return;

        string path = Write("made.sav", since.AddHours(-1));
        File.SetCreationTimeUtc(path, since.AddSeconds(1));

        Assert.Single(Find());
    }

    // Steam Deck: il prefisso di Proton del gioco sta dentro la cartella di Steam, che si salta.
    [Fact]
    public void SkippedFolderAroundTheRootDoesNotHideTheRoot()
    {
        string steam = Path.Combine(folder.Root, "Steam");
        string prefix = Path.Combine(steam, "steamapps", "compatdata", "367520", "pfx");
        string save = Path.Combine(prefix, "drive_c", "users", "steamuser", "user1.dat");
        TestFolder.Write(save, "x");
        File.SetLastWriteTimeUtc(save, since.AddSeconds(5));
        string other = Path.Combine(steam, "logs2", "other.sav");
        TestFolder.Write(other, "x");
        File.SetLastWriteTimeUtc(other, since.AddSeconds(5));

        IReadOnlyList<ChangedFile> found = SaveFinder.FindChanged(
            [new SearchRoot(prefix, 8, true), new SearchRoot(folder.Root, 8, false)], since, [steam],
            TimeSpan.FromMinutes(1), CancellationToken.None, out _);

        ChangedFile file = Assert.Single(found);
        Assert.Equal(save, file.Path);
    }

    [Fact]
    public void SystemFolderNamesAreNotRead()
    {
        Write(Path.Combine("Cache", "a.sav"), since.AddSeconds(5));
        Write(Path.Combine("nvidia", "b.sav"), since.AddSeconds(5));
        string kept = Write(Path.Combine("saves", "c.sav"), since.AddSeconds(5));

        ChangedFile found = Assert.Single(Find());

        Assert.Equal(kept, found.Path);
    }

    [Fact]
    public void DepthLimitIsRespected()
    {
        string shallow = Write(Path.Combine("a", "b", "file.sav"), since.AddSeconds(5));
        Write(Path.Combine("a", "b", "c", "deep.sav"), since.AddSeconds(5));

        ChangedFile found = Assert.Single(Find(depth: 2));

        Assert.Equal(shallow, found.Path);
    }

    [Fact]
    public void IgnoredExtensionsAreNotSaves()
    {
        Write("game.log", since.AddSeconds(5));
        Write("game.tmp", since.AddSeconds(5));
        Write("game.dmp", since.AddSeconds(5));
        Write("game.sav~", since.AddSeconds(5));

        Assert.Empty(Find());
    }

    [Fact]
    public void CancelledSearchReturnsPartialResult()
    {
        Write("new.sav", since.AddSeconds(5));
        var cancel = new CancellationTokenSource();
        cancel.Cancel();

        IReadOnlyList<ChangedFile> found = SaveFinder.FindChanged(
            [new SearchRoot(Root, 8, true)], since, [], TimeSpan.FromMinutes(1), cancel.Token, out bool partial);

        Assert.True(partial);
        Assert.Empty(found);
    }

    [Fact]
    public void CompletedSearchIsNotPartial()
    {
        Write("new.sav", since.AddSeconds(5));

        SaveFinder.FindChanged([new SearchRoot(Root, 8, true)], since, [], TimeSpan.FromMinutes(1),
            CancellationToken.None, out bool partial);

        Assert.False(partial);
    }

    [Fact]
    public void SkippedFoldersAreNotEntered()
    {
        string ignored = Path.Combine(Root, "Launcher");
        Write(Path.Combine("Launcher", "sub", "x.sav"), since.AddSeconds(5));
        Write(Path.Combine("saves", "y.sav"), since.AddSeconds(5));

        var visited = new List<string>();

        IReadOnlyList<ChangedFile> found = SaveFinder.Scan(
            [new SearchRoot(Root, 8, true)], since, [ignored], TimeSpan.FromMinutes(1), CancellationToken.None,
            out _, visited);

        Assert.Single(found);
        Assert.DoesNotContain(visited, path => path.StartsWith(ignored, StringComparison.Ordinal));
    }

    [Fact]
    public void FileFoundInTwoRootsIsListedOnce()
    {
        string inner = Path.Combine(Root, "saves");
        Write(Path.Combine("saves", "z.sav"), since.AddSeconds(5));

        IReadOnlyList<ChangedFile> found = SaveFinder.FindChanged(
            [new SearchRoot(Root, 8, true), new SearchRoot(inner, 8, false)], since, [], TimeSpan.FromMinutes(1),
            CancellationToken.None, out _);

        ChangedFile file = Assert.Single(found);
        Assert.Equal(new SearchRoot(Root, 8, true), file.Root);
    }

    [Fact]
    public void SymbolicLinksAreNotFollowed()
    {
        string outside = Path.Combine(folder.Root, "outside");
        TestFolder.Write(Path.Combine(outside, "far.sav"), "x");
        File.SetLastWriteTimeUtc(Path.Combine(outside, "far.sav"), since.AddSeconds(5));
        Directory.CreateDirectory(Root);

        try
        {
            Directory.CreateSymbolicLink(Path.Combine(Root, "link"), outside);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // Senza permesso di creare collegamenti (Windows senza modalità sviluppatore): niente da provare.
            return;
        }

        Assert.Empty(Find());
    }
}
