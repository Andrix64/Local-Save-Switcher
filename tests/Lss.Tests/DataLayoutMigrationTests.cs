namespace Lss.Tests;

/// <summary>
/// DataLayoutMigration: le cartelle delle beta (dati\IDAccount) vanno sotto
/// dati\steam\, senza perdere né sovrascrivere nulla.
/// </summary>
public sealed class DataLayoutMigrationTests : IDisposable
{
    private readonly TestFolder folder = new();

    private string Root => folder.Data;

    public void Dispose() => folder.Dispose();

    [Fact]
    public void NumericFoldersMoveUnderSteam()
    {
        TestFolder.Write(Path.Combine(Root, "111", "870780", "Info", "a.txt"), "x");

        DataLayoutMigration.Run(Root);

        Assert.Equal("x", File.ReadAllText(Path.Combine(Root, "steam", "111", "870780", "Info", "a.txt")));
        Assert.False(Directory.Exists(Path.Combine(Root, "111")));
    }

    [Fact]
    public void SecondRunChangesNothing()
    {
        TestFolder.Write(Path.Combine(Root, "111", "870780", "a.txt"), "x");

        DataLayoutMigration.Run(Root);
        DataLayoutMigration.Run(Root);

        Assert.Equal("x", File.ReadAllText(Path.Combine(Root, "steam", "111", "870780", "a.txt")));
        Assert.Equal(["steam"], Directory.GetDirectories(Root).Select(Path.GetFileName));
    }

    [Fact]
    public void ExistingTargetIsNeverOverwritten()
    {
        TestFolder.Write(Path.Combine(Root, "111", "870780", "a.txt"), "vecchio");
        TestFolder.Write(Path.Combine(Root, "steam", "111", "870780", "a.txt"), "nuovo");

        DataLayoutMigration.Run(Root);

        Assert.Equal("nuovo", File.ReadAllText(Path.Combine(Root, "steam", "111", "870780", "a.txt")));
        Assert.Equal("vecchio", File.ReadAllText(Path.Combine(Root, "111", "870780", "a.txt")));
    }

    [Fact]
    public void InterruptedMoveIsFinished()
    {
        // Spostamento fermato dopo la copia: la destinazione è identica e l'originale c'è ancora.
        TestFolder.Write(Path.Combine(Root, "111", "870780", "a.txt"), "x");
        TestFolder.Write(Path.Combine(Root, "steam", "111", "870780", "a.txt"), "x");

        DataLayoutMigration.Run(Root);

        Assert.False(Directory.Exists(Path.Combine(Root, "111")));
        Assert.Equal("x", File.ReadAllText(Path.Combine(Root, "steam", "111", "870780", "a.txt")));
    }

    [Fact]
    public void FilesAndOtherFoldersStayWhereTheyAre()
    {
        TestFolder.Write(Path.Combine(Root, "lss.ini"), "UltimoAccount=111");
        TestFolder.Write(Path.Combine(Root, "log.txt"), "log");
        TestFolder.Write(Path.Combine(Root, "Altre", "x.txt"), "x");

        DataLayoutMigration.Run(Root);

        Assert.Equal("UltimoAccount=111", File.ReadAllText(Path.Combine(Root, "lss.ini")));
        Assert.Equal("log", File.ReadAllText(Path.Combine(Root, "log.txt")));
        Assert.Equal("x", File.ReadAllText(Path.Combine(Root, "Altre", "x.txt")));
    }

    [Fact]
    public void MissingDataFolderDoesNothing()
    {
        Assert.Null(Record.Exception(() => DataLayoutMigration.Run(Path.Combine(Root, "non-esiste"))));
    }
}
