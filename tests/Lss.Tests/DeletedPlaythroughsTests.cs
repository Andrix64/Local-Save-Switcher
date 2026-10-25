namespace Lss.Tests;

public sealed class DeletedPlaythroughsTests : IDisposable
{
    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    [Fact]
    public void PruningKeepsDeletedPlaythroughsAndDoesNotCountThem()
    {
        SaveManager manager = folder.CreateManager("A1");
        manager.CreateProfile("Mario", false, TestFolder.Owner);
        manager.DeleteProfile("Profilo_Mario");
        manager.SetBackupsToKeep(2);

        for (int i = 0; i < 5; i++)
            manager.CreateProfile("Copia" + i, true, TestFolder.Owner);

        Assert.Single(manager.GetDeletedProfiles());
        Assert.Equal(2, manager.GetBackups().Count(backup => backup.Kind != BackupKind.Delete));
    }

    [Fact]
    public void PurgeDeletesTheFolderAndItsInfoOnly()
    {
        SaveManager manager = folder.CreateManager("A1");
        manager.CreateProfile("Mario", false, TestFolder.Owner);
        manager.CreateProfile("Luigi", false, TestFolder.Owner);
        manager.DeleteProfile("Profilo_Mario");
        manager.DeleteProfile("Profilo_Luigi");
        Snapshot mario = manager.GetDeletedProfiles().Single(item => item.Owner == "Profilo_Mario");

        Result result = manager.PurgeDeletedProfile(mario);

        Assert.True(result.Success, result.Message);
        Assert.False(Directory.Exists(mario.Path));
        Assert.False(File.Exists(mario.Path + ".ini"));
        Assert.Equal("Profilo_Luigi", Assert.Single(manager.GetDeletedProfiles()).Owner);
    }

    [Fact]
    public void PurgeRefusesAFolderOutsideTheBackups()
    {
        SaveManager manager = folder.CreateManager("A1");
        manager.CreateProfile("Mario", false, TestFolder.Owner);
        manager.DeleteProfile("Profilo_Mario");
        Snapshot deleted = manager.GetDeletedProfiles().Single();
        string outside = Path.Combine(folder.Root, "fuori");
        Directory.CreateDirectory(outside);

        Result result = manager.PurgeDeletedProfile(deleted with { Path = outside });

        Assert.False(result.Success);
        Assert.True(Directory.Exists(outside));
    }

    [Fact]
    public void TheOwnerOfADeletedPlaythroughIsKept()
    {
        SaveManager manager = folder.CreateManager("A1");
        manager.CreateProfile("Mario", false, TestFolder.Owner);
        manager.DeleteProfile("Profilo_Mario");

        Assert.Equal(TestFolder.Owner, manager.DeletedOwnerOf(manager.GetDeletedProfiles().Single()));
    }
}
