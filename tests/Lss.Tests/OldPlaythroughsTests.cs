using System.IO.Compression;

namespace Lss.Tests;

public sealed class OldPlaythroughsTests : IDisposable
{
    private const string Second = "b2c3d4e5";

    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    /// <summary>Manager con due persone: TestFolder.Owner (admin, "Andrea") e Second (utente, "Clara").</summary>
    private SaveManager ManagerWithTwoPlayers()
    {
        SaveManager manager = folder.CreateManager("A1");
        TestFolder.WritePeople(folder.Data,
            $"Ordine={TestFolder.Owner},{Second}\n" +
            $"{TestFolder.Owner}.Nome=Andrea\n{TestFolder.Owner}.Ruolo=admin\n" +
            $"{Second}.Nome=Clara\n{Second}.Ruolo=utente\n");

        var people = new People(folder.Data);
        people.Load();
        manager.Players = people;
        return manager;
    }

    /// <summary>
    /// Toglie il proprietario alla partita, come nelle partite fatte prima di LSS 2.0.
    /// </summary>
    private void StripOwner(string profile)
    {
        IniFile.Write(Path.Combine(folder.Data, "Info", profile + ".ini"),
            [new KeyValuePair<string, string>("Nota", "")]);
    }

    /// <summary>Stato salvato su disco, riletto come farebbe il prossimo avvio.</summary>
    private bool OwnersAssignedOnDisk()
    {
        var state = new AppState(Path.Combine(folder.Data, "stato.ini"));
        state.Load();
        return state.OwnersAssigned;
    }

    [Fact]
    public void OldPlaythroughsGetTheFirstAdminAndAFavouriteOnce()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        manager.CreateProfile("Luigi", false, TestFolder.Owner);
        StripOwner("Profilo_Mario");
        StripOwner("Profilo_Luigi");
        File.Delete(Path.Combine(folder.Data, "preferite.ini"));

        manager.AssignExistingPlaythroughs();

        Assert.Equal(TestFolder.Owner, manager.GetInfo("Profilo_Mario").Owner);
        Assert.Equal(TestFolder.Owner, manager.GetInfo("Profilo_Luigi").Owner);
        Assert.NotNull(manager.FavouriteOf(TestFolder.Owner));
        Assert.True(OwnersAssignedOnDisk());
    }

    [Fact]
    public void ASecondRunOnlyGivesOwnerlessPlaythroughsToTheFirstAdmin()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        manager.CreateProfile("Luigi", false, Second);
        manager.AssignExistingPlaythroughs();
        Assert.True(OwnersAssignedOnDisk());

        // Dopo ProprietariOk il passo delle preferite non si rifà, ma una partita senza
        // persona valida va al primo amministratore a ogni apertura (anche una recuperata).
        StripOwner("Profilo_Mario");
        manager.AssignExistingPlaythroughs();

        Assert.Equal(TestFolder.Owner, manager.GetInfo("Profilo_Mario").Owner);
        Assert.Equal(Second, manager.GetInfo("Profilo_Luigi").Owner);
    }

    [Fact]
    public void RecoveryGoesBackToThePlayerWhenTheyAreStillThere()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        manager.CreateProfile("Luigi", false, Second);
        Assert.True(manager.DeleteProfile("Profilo_Luigi").Success);
        Snapshot deleted = Assert.Single(manager.GetDeletedProfiles());

        Assert.Equal(Second, manager.RecoveryOwnerOf(deleted));
    }

    [Fact]
    public void RecoveryGoesToTheFirstAdminWhenThePlayerIsGone()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        manager.CreateProfile("Luigi", false, Second);
        Assert.True(manager.DeleteProfile("Profilo_Luigi").Success);
        Snapshot deleted = Assert.Single(manager.GetDeletedProfiles());

        // Clara non esiste più: la partita va al primo amministratore, come le partite vecchie.
        TestFolder.WritePeople(folder.Data,
            $"Ordine={TestFolder.Owner}\n{TestFolder.Owner}.Nome=Andrea\n{TestFolder.Owner}.Ruolo=admin\n");
        var people = new People(folder.Data);
        people.Load();
        manager.Players = people;

        Assert.Equal(TestFolder.Owner, manager.RecoveryOwnerOf(deleted));
        Assert.True(manager.RecoverDeletedProfile(deleted, manager.RecoveryOwnerOf(deleted)).Success);
        Assert.Equal(TestFolder.Owner, manager.GetInfo("Profilo_Luigi").Owner);
        Assert.Equal("Profilo_Luigi", manager.FavouriteOf(TestFolder.Owner));
    }

    [Fact]
    public void TheActivePlaythroughBecomesTheFavourite()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        manager.CreateProfile("Luigi", false, TestFolder.Owner);
        Assert.True(manager.ActivateProfile("Profilo_Luigi").Success);
        StripOwner("Profilo_Mario");
        StripOwner("Profilo_Luigi");
        File.Delete(Path.Combine(folder.Data, "preferite.ini"));

        manager.AssignExistingPlaythroughs();

        Assert.Equal("Profilo_Luigi", manager.FavouriteOf(TestFolder.Owner));
    }

    [Fact]
    public void WithoutPeopleNothingIsMarked()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        StripOwner("Profilo_Mario");
        manager.Players = null;

        manager.AssignExistingPlaythroughs();

        Assert.Equal("", manager.GetInfo("Profilo_Mario").Owner);
        Assert.False(OwnersAssignedOnDisk());
    }

    [Fact]
    public void ExportWritesThePlayerAndTheirName()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        string zip = Path.Combine(folder.Root, "mario.zip");
        Assert.True(manager.ExportProfile("Profilo_Mario", zip).Success);

        using ZipArchive archive = ZipFile.OpenRead(zip);
        using var reader = new StreamReader(archive.GetEntry("partita.ini")!.Open());
        string info = reader.ReadToEnd();

        Assert.Contains($"Giocatore={TestFolder.Owner}", info);
        Assert.Contains("NomeGiocatore=Andrea", info);
    }
}
