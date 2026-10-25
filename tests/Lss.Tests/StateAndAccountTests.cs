namespace Lss.Tests;

public sealed class StateAndAccountTests : IDisposable
{
    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    [Fact]
    public void AppState_RoundTripsAndLeavesNoTempFile()
    {
        string path = Path.Combine(folder.Root, "stato.ini");
        var state = new AppState(path)
        {
            ActiveProfile = "Profilo_Mario",
            ActiveSince = "22/10/2026 18:30:00",
            LastSaved = "",
            BackupsToKeep = 5
        };

        state.Save();

        var loaded = new AppState(path);
        loaded.Load();

        Assert.Equal("Profilo_Mario", loaded.ActiveProfile);
        Assert.Equal("22/10/2026 18:30:00", loaded.ActiveSince);
        Assert.Equal("", loaded.LastSaved);
        Assert.Equal(5, loaded.BackupsToKeep);
        Assert.False(File.Exists(path + ".tmp"));
        Assert.Contains("SalvatoIl=-", File.ReadAllLines(path));
    }

    [Fact]
    public void AppState_UsesDefaultsForMissingOrInvalidValues()
    {
        string path = Path.Combine(folder.Root, "stato.ini");
        File.WriteAllLines(path, new[] { "ProfiloAttivo=Profilo_Mario", "BackupDaConservare=abc" });

        var state = new AppState(path);
        state.Load();

        Assert.Equal("Profilo_Mario", state.ActiveProfile);
        Assert.Equal(AppState.DefaultBackupsToKeep, state.BackupsToKeep);
    }

    [Fact]
    public void FindAccounts_ListsOnlyAccountsWithControlSaves()
    {
        string userData = Path.Combine(folder.Root, "userdata");
        string oldSave = Path.Combine(userData, "111", "870780", "remote", "a.sav");

        TestFolder.Write(oldSave, "a");
        File.SetLastWriteTime(oldSave, DateTime.Now.AddDays(-3));
        TestFolder.Write(Path.Combine(userData, "111", "config", "localconfig.vdf"),
            "\"UserLocalConfigStore\"\n{\n\t\"friends\"\n\t{\n\t\t\"PersonaName\"\t\t\"Andrix\"\n\t}\n}");
        TestFolder.Write(Path.Combine(userData, "222", "870780", "remote", "b.sav"), "b");
        Directory.CreateDirectory(Path.Combine(userData, "333", "999999"));

        IReadOnlyList<GameAccount> accounts = SaveManager.FindAccounts(userData, "870780");

        Assert.Equal(new[] { "222", "111" }, accounts.Select(a => a.Id));
        Assert.Equal("Andrix", accounts[1].Name);
        Assert.Equal("Account 222", accounts[0].DisplayName);
    }

    [Theory]
    [InlineData("Mario", true)]
    [InlineData("Mario_2-b", true)]
    [InlineData("Mário", true)]
    [InlineData("Partita principale", true)]
    [InlineData(" spazio iniziale", false)]
    [InlineData("a/b", false)]
    [InlineData("punto.finale", false)]
    [InlineData("", false)]
    public void IsValidName(string name, bool expected)
    {
        Assert.Equal(expected, SaveManager.IsValidName(name));
    }
}
