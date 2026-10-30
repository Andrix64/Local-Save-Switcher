namespace Lss.Tests;

/// <summary>
/// Rinominare una partita porta con sé le sue copie automatiche (la cronologia).
/// </summary>
public sealed class RenameKeepsBackupsTests : IDisposable
{
    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    /// <summary>Mario attivo, con due salvataggi: due copie "prima del salvataggio".</summary>
    private SaveManager ManagerWithTwoCopies()
    {
        SaveManager manager = folder.CreateManager("A1");
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        manager.ActivateProfile("Profilo_Mario");

        TestFolder.Write(folder.SaveFile, "A2");
        Assert.True(manager.SaveProgress().Success);
        TestFolder.Write(folder.SaveFile, "A3");
        Assert.True(manager.SaveProgress().Success);

        return manager;
    }

    [Fact]
    public void RenamedPlaythroughKeepsItsCopies()
    {
        SaveManager manager = ManagerWithTwoCopies();
        int before = manager.GetBackupsOf("Profilo_Mario").Count;
        Assert.True(before >= 2);

        Result result = manager.RenameProfile("Profilo_Mario", "Mario Rossi");

        Assert.True(result.Success, result.Message);
        Assert.Equal(before, manager.GetBackupsOf("Profilo_Mario Rossi").Count);
        Assert.Empty(manager.GetBackupsOf("Profilo_Mario"));
    }

    [Fact]
    public void CopyWithoutInfoFileGetsOne()
    {
        SaveManager manager = ManagerWithTwoCopies();
        Snapshot copy = manager.GetBackupsOf("Profilo_Mario")[0];
        File.Delete(copy.Path + ".json");

        Assert.True(manager.RenameProfile("Profilo_Mario", "Luigi").Success);

        Assert.True(File.Exists(copy.Path + ".json"));
        Assert.Contains(manager.GetBackupsOf("Profilo_Luigi"), b => b.Path == copy.Path);
    }

    [Fact]
    public void CopiesOfADeletedPlaythroughWithTheOldNameAreNotTouched()
    {
        SaveManager manager = ManagerWithTwoCopies();
        manager.CreateProfile("Luigi", false, TestFolder.Owner);
        manager.ActivateProfile("Profilo_Luigi");

        // Mario viene eliminato: la sua copia è di una partita che non c'è più.
        Assert.True(manager.DeleteProfile("Profilo_Mario").Success);
        Snapshot deleted = Assert.Single(manager.GetDeletedProfiles(), b => b.Owner == "Profilo_Mario");

        // Un'altra partita prende il nome "Mario".
        Assert.True(manager.RenameProfile("Profilo_Luigi", "Mario").Success);

        Assert.Contains(manager.GetDeletedProfiles(), b => b.Path == deleted.Path && b.Owner == "Profilo_Mario");
        Assert.DoesNotContain(manager.GetBackupsOf("Profilo_Mario"), b => b.Path == deleted.Path);
    }

    [Fact]
    public void CaseOnlyRenameKeepsTheCopies()
    {
        SaveManager manager = ManagerWithTwoCopies();
        int before = manager.GetBackupsOf("Profilo_Mario").Count;

        Result result = manager.RenameProfile("Profilo_Mario", "mario");

        Assert.True(result.Success, result.Message);
        Assert.Equal(before, manager.GetBackupsOf("Profilo_mario").Count);
        Assert.Empty(manager.GetBackupsOf("Profilo_Mario"));
    }

    [Fact]
    public void RenamingTheActivePlaythroughKeepsCopiesAndState()
    {
        SaveManager manager = ManagerWithTwoCopies();
        Assert.Equal("Profilo_Mario", manager.State.ActiveProfile);
        int before = manager.GetBackupsOf("Profilo_Mario").Count;

        Assert.True(manager.RenameProfile("Profilo_Mario", "Anna").Success);

        Assert.Equal("Profilo_Anna", manager.State.ActiveProfile);
        Assert.Equal(before, manager.GetBackupsOf("Profilo_Anna").Count);
    }

    [Fact]
    public void ACopyThatCannotBeUpdatedGivesAWarningButTheRenameWorks()
    {
        SaveManager manager = ManagerWithTwoCopies();
        Snapshot blocked = manager.GetBackupsOf("Profilo_Mario")[0];

        // Una cartella al posto del file temporaneo: la scrittura dell'info fallisce.
        Directory.CreateDirectory(blocked.Path + ".json.tmp");

        Result result = manager.RenameProfile("Profilo_Mario", "Anna");

        Assert.True(manager.ProfileExists("Profilo_Anna"));
        Assert.False(manager.ProfileExists("Profilo_Mario"));
        Assert.False(result.Success);
        Assert.Contains(Path.GetFileName(blocked.Path), result.Message);
        Assert.Contains(Loc.F(Strings.BackupsNotMoved, "").TrimEnd(), result.Message);
    }
}
