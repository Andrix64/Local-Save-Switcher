using System.Globalization;

namespace Lss.Tests;

public sealed class SnapshotTests : IDisposable
{
    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    /// <summary>
    /// Manager con il profilo "Mario" attivo e i salvataggi "A1".
    /// </summary>
    private SaveManager ManagerWithActiveProfile()
    {
        SaveManager manager = folder.CreateManager("A1");
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        manager.ActivateProfile("Profilo_Mario");
        return manager;
    }

    [Fact]
    public void CreateSavestate_DoesNotTouchProfile()
    {
        SaveManager manager = ManagerWithActiveProfile();
        TestFolder.Write(folder.SaveFile, "A2");

        Result result = manager.CreateSavestate("Prima di Ahti");

        Assert.True(result.Success, result.Message);
        Snapshot savestate = Assert.Single(manager.GetSavestates());
        Assert.Equal("Prima di Ahti", savestate.Name);
        Assert.Equal("A2", File.ReadAllText(Path.Combine(savestate.Path, "remote", "slot.sav")));
        Assert.Equal("A1", File.ReadAllText(
            Path.Combine(manager.ProfilesPath, "Profilo_Mario", "remote", "slot.sav")));
    }

    [Theory]
    [InlineData("a/b")]
    [InlineData("nome lungo più di cinquanta caratteri, quindi non valido")]
    public void CreateSavestate_RejectsInvalidNames(string name)
    {
        SaveManager manager = ManagerWithActiveProfile();

        Assert.False(manager.CreateSavestate(name).Success);
        Assert.Empty(manager.GetSavestates());
    }

    [Fact]
    public void CreateSavestate_RequiresActiveProfile()
    {
        SaveManager manager = folder.CreateManager();

        Assert.False(manager.CreateSavestate("").Success);
    }

    [Fact]
    public void RestoreSavestate_PutsSavesBackAndKeepsActiveProfile()
    {
        SaveManager manager = ManagerWithActiveProfile();
        TestFolder.Write(folder.SaveFile, "prima del boss");
        manager.CreateSavestate("Boss");
        TestFolder.Write(folder.SaveFile, "morto");

        Result result = manager.RestoreSavestate(manager.GetSavestates()[0]);

        Assert.True(result.Success, result.Message);
        Assert.Equal("prima del boss", File.ReadAllText(folder.SaveFile));
        Assert.Equal("cache", File.ReadAllText(folder.CacheFile));
        Assert.Equal("Profilo_Mario", manager.State.ActiveProfile);

        Snapshot backup = manager.GetBackups()[0];
        Assert.Equal("Prima_ripristino", backup.Name);
        Assert.Equal("morto", File.ReadAllText(Path.Combine(backup.Path, "remote", "slot.sav")));
    }

    [Fact]
    public void DeleteSavestate_RefusesPathsOutsideSavestateFolder()
    {
        SaveManager manager = ManagerWithActiveProfile();

        Assert.False(manager.DeleteSavestate(new Snapshot(folder.Root, DateTime.Now, "")).Success);
        Assert.True(Directory.Exists(folder.Root));
    }

    [Fact]
    public void Savestates_BelongToTheirProfile()
    {
        SaveManager manager = ManagerWithActiveProfile();
        manager.CreateSavestate("Boss");
        manager.CreateProfile("Luigi", true, TestFolder.Owner);
        manager.ActivateProfile("Profilo_Luigi");

        Assert.Empty(manager.GetSavestates());
        Assert.Equal(1, manager.CountSavestates("Profilo_Mario"));
    }

    [Fact]
    public void RestoreBackup_PutsBackupIntoGame()
    {
        SaveManager manager = ManagerWithActiveProfile();
        Snapshot beforeActivation = manager.GetBackups()
            .First(b => b.Name == "Prima_di_Profilo_Mario");
        TestFolder.Write(folder.SaveFile, "altro");

        Result result = manager.RestoreBackup(beforeActivation);

        Assert.True(result.Success, result.Message);
        Assert.Equal("A1", File.ReadAllText(folder.SaveFile));
    }

    [Fact]
    public void SetBackupsToKeep_DeletesOldestAndIsSaved()
    {
        SaveManager manager = ManagerWithActiveProfile();
        manager.SaveProgress(allowEmptySaves: true);
        manager.SaveProgress(allowEmptySaves: true);
        Assert.True(manager.GetBackups().Count > 2);
        string newest = manager.GetBackups()[0].Path;

        Assert.True(manager.SetBackupsToKeep(2).Success);

        Assert.Equal(2, manager.GetBackups().Count);
        Assert.Equal(newest, manager.GetBackups()[0].Path);

        manager.ReloadState();
        Assert.Equal(2, manager.State.BackupsToKeep);
    }

    [Fact]
    public void Operations_PruneBackupsAutomatically()
    {
        SaveManager manager = ManagerWithActiveProfile();
        manager.SetBackupsToKeep(1);

        manager.SaveProgress(allowEmptySaves: true);
        manager.SaveProgress(allowEmptySaves: true);

        Assert.Single(manager.GetBackups());
    }

    [Theory]
    [InlineData("Prima_di_Profilo_Mario", "Prima di attivare Mario")]
    [InlineData("Prima_salvataggio_Profilo_Mario", "Profilo Mario prima del salvataggio")]
    [InlineData("Eliminato_Profilo_Mario", "Profilo Mario eliminato")]
    [InlineData("Prima_ripristino", "Prima di un ripristino")]
    [InlineData("Prima_creazione", "Prima di creare un profilo")]
    public void BackupReasonText_IsReadable(string reason, string expected)
    {
        Assert.Equal(expected, SaveManager.BackupReasonText(reason));
    }

    // Con un calendario non gregoriano (th-TH: anno 2569) i nomi delle cartelle devono restare leggibili.
    [Fact]
    public void PointsAndBackupsAreFoundWithANonGregorianCulture()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("th-TH");
            SaveManager manager = ManagerWithActiveProfile();
            TestFolder.Write(folder.SaveFile, "A2");

            Assert.True(manager.CreateSavestate("Punto").Success);
            Assert.True(manager.SaveProgress().Success);

            Snapshot point = Assert.Single(manager.GetSavestates());
            Assert.Equal(DateTime.Now.Year, point.Date.Year);
            Assert.Contains(manager.GetBackups(), backup => backup.Kind == BackupKind.Save && backup.Date.Year == DateTime.Now.Year);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
