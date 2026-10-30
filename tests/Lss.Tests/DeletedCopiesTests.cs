namespace Lss.Tests;

/// <summary>
/// Le copie automatiche di una partita eliminata restano con lei: una partita nuova
/// con lo stesso nome non le eredita, e il recupero le riporta.
/// </summary>
public sealed class DeletedCopiesTests : IDisposable
{
    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    /// <summary>Mario eliminato, con almeno una copia; "Altro" è la partita attiva.</summary>
    private SaveManager ManagerWithDeletedMario(out int copies)
    {
        SaveManager manager = folder.CreateManager("A1");
        Assert.True(manager.AdoptGameSaves("Mario", TestFolder.Owner).Success);
        TestFolder.Write(folder.SaveFile, "A2");
        Assert.True(manager.SaveProgress().Success);
        Assert.True(manager.CreateProfile("Altro", false, TestFolder.Owner).Success);
        Assert.True(manager.ActivateProfile("Profilo_Altro").Success);
        copies = manager.GetBackupsOf("Profilo_Mario").Count;
        Assert.True(copies > 0);

        Result deleted = manager.DeleteProfile("Profilo_Mario");
        Assert.True(deleted.Success, deleted.Message);

        return manager;
    }

    [Fact]
    public void ANewPlaythroughWithTheNameOfADeletedOneHasNoCopies()
    {
        SaveManager manager = ManagerWithDeletedMario(out _);

        Assert.True(manager.CreateProfile("Mario", false, TestFolder.Owner).Success);

        Assert.Empty(manager.GetBackupsOf("Profilo_Mario"));
        Assert.Empty(V1Compat.BackupsWithoutOwner(manager));
    }

    [Fact]
    public void RecoveringADeletedPlaythroughBringsItsCopiesBack()
    {
        SaveManager manager = ManagerWithDeletedMario(out int copies);
        Assert.True(manager.CreateProfile("Mario", false, TestFolder.Owner).Success);
        Snapshot deleted = manager.GetDeletedProfiles().Single(item => item.Owner == "Profilo_Mario");

        Result result = manager.RecoverDeletedProfile(deleted, TestFolder.Owner);

        Assert.True(result.Success, result.Message);
        Assert.Equal(copies, manager.GetBackupsOf("Profilo_Mario 2").Count);
        Assert.Empty(manager.GetBackupsOf("Profilo_Mario"));
    }

    [Fact]
    public void ACopyThatCannotBeUpdatedGivesAWarningButTheDeleteWorks()
    {
        SaveManager manager = folder.CreateManager("A1");
        Assert.True(manager.AdoptGameSaves("Mario", TestFolder.Owner).Success);
        TestFolder.Write(folder.SaveFile, "A2");
        Assert.True(manager.SaveProgress().Success);
        Assert.True(manager.CreateProfile("Altro", false, TestFolder.Owner).Success);
        Assert.True(manager.ActivateProfile("Profilo_Altro").Success);
        Snapshot blocked = manager.GetBackupsOf("Profilo_Mario")[0];

        // Una cartella al posto del file temporaneo: la scrittura dell'info fallisce.
        Directory.CreateDirectory(blocked.Path + ".json.tmp");

        Result result = manager.DeleteProfile("Profilo_Mario");

        Assert.False(manager.ProfileExists("Profilo_Mario"));
        Assert.Single(manager.GetDeletedProfiles());
        Assert.False(result.Success);
        Assert.Contains(Path.GetFileName(blocked.Path), result.Message);
    }
}
