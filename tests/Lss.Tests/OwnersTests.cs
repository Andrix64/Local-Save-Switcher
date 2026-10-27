namespace Lss.Tests;

public sealed class OwnersTests : IDisposable
{
    private const string Second = "b2c3d4e5";

    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    /// <summary>Manager con due persone: TestFolder.Owner (admin) e Second (utente).</summary>
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

    [Fact]
    public void CreateSetsTheOwner()
    {
        SaveManager manager = ManagerWithTwoPlayers();

        Assert.True(manager.CreateProfile("Mario", true, TestFolder.Owner).Success);
        Assert.True(manager.CreateProfile("Luigi", false, Second).Success);

        Assert.Equal(TestFolder.Owner, manager.GetInfo("Profilo_Mario").Owner);
        Assert.Equal(Second, manager.GetInfo("Profilo_Luigi").Owner);
    }

    [Fact]
    public void UnknownOwnerIsRefused()
    {
        SaveManager manager = ManagerWithTwoPlayers();

        Result result = manager.CreateProfile("Mario", true, "ffffffff");

        Assert.False(result.Success);
        Assert.False(manager.ProfileExists("Profilo_Mario"));
    }

    [Fact]
    public void EmptyOwnerIsRefused()
    {
        SaveManager manager = ManagerWithTwoPlayers();

        Assert.False(manager.CreateProfile("Mario", false, "").Success);
    }

    [Fact]
    public void AdoptSetsTheOwner()
    {
        SaveManager manager = ManagerWithTwoPlayers();

        Result result = manager.AdoptGameSaves("Partita principale", Second);

        Assert.True(result.Success, result.Message);
        Assert.Equal(Second, manager.GetInfo("Profilo_Partita principale").Owner);
    }

    [Fact]
    public void ImportSetsTheOwnerChosenNow()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        string zip = Path.Combine(folder.Root, "mario.zip");
        Assert.True(manager.ExportProfile("Profilo_Mario", zip).Success);

        Result<string> imported = manager.ImportProfile(zip, Second);

        Assert.True(imported.Success, imported.Message);
        Assert.Equal(Second, manager.GetInfo(imported.Value!).Owner);
        Assert.Equal(TestFolder.Owner, manager.GetInfo("Profilo_Mario").Owner);
    }

    [Fact]
    public void RecoverSetsTheOwnerChosenNow()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        Assert.True(manager.DeleteProfile("Profilo_Mario").Success);
        Snapshot deleted = Assert.Single(manager.GetDeletedProfiles());

        Result result = manager.RecoverDeletedProfile(deleted, Second);

        Assert.True(result.Success, result.Message);
        Assert.Equal(Second, manager.GetInfo("Profilo_Mario").Owner);
    }

    [Fact]
    public void RenameKeepsTheOwner()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        manager.CreateProfile("Mario", true, Second);

        Assert.True(manager.RenameProfile("Profilo_Mario", "Bowser").Success);

        Assert.Equal(Second, manager.GetInfo("Profilo_Bowser").Owner);
    }

    [Fact]
    public void GetProfilesOfListsOnlyThePlayersPlaythroughs()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        manager.CreateProfile("Luigi", false, Second);
        manager.CreateProfile("Peach", false, TestFolder.Owner);

        Assert.Equal(new[] { "Profilo_Mario", "Profilo_Peach" }, manager.GetProfilesOf(TestFolder.Owner));
        Assert.Equal(new[] { "Profilo_Luigi" }, manager.GetProfilesOf(Second));
    }

    [Fact]
    public void SetOwnerGivesThePlaythroughToAnotherPlayer()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        manager.CreateProfile("Mario", true, TestFolder.Owner);

        Assert.True(manager.SetOwner("Profilo_Mario", Second).Success);

        Assert.Equal(Second, manager.GetInfo("Profilo_Mario").Owner);
        Assert.Empty(manager.GetProfilesOf(TestFolder.Owner));
    }

    [Fact]
    public void SetOwnerRefusesAnUnknownPlayer()
    {
        SaveManager manager = ManagerWithTwoPlayers();
        manager.CreateProfile("Mario", true, TestFolder.Owner);

        Assert.False(manager.SetOwner("Profilo_Mario", "ffffffff").Success);
        Assert.Equal(TestFolder.Owner, manager.GetInfo("Profilo_Mario").Owner);
    }
}
