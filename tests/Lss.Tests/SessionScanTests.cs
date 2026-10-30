namespace Lss.Tests;

/// <summary>
/// SessionScan: dopo una partita, le cartelle cambiate che sembrano salvataggi (N10).
/// </summary>
public sealed class SessionScanTests : IDisposable
{
    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    private string Home => Path.Combine(folder.Root, "home");
    private string Documents => Path.Combine(Home, "Documents");
    private string Install => Path.Combine(folder.Root, "lib", "steamapps", "common", "Hollow");
    private string Lss => Path.Combine(folder.Root, "lssdata");

    private SaveContext Context() => new(
        new KnownFolders(Home, Path.Combine(Home, "AppData", "Roaming"), Path.Combine(Home, "AppData", "Local"),
            Path.Combine(Home, "AppData", "LocalLow"), Documents, Path.Combine(Home, "Saved Games"), "", "",
            Path.Combine(Home, ".local", "share"), Path.Combine(Home, ".config"), "andrix"),
        "",
        false,
        Install,
        "123",
        IgnoreCase: false);

    private GameInfo Game() => new("steam", "367520", "Hollow Knight", "Hollow", "", Install,
        [new SaveSpot(1, folder.Game, "", true, folder.Game, SaveSource.Userdata)], SupportState.Supported);

    private IReadOnlyList<SpotSuggestion> Run(GameSpots spots, DateTime sessionStart) =>
        SessionScan.Run(Game(), Context(), sessionStart, spots, [], Lss, CancellationToken.None);

    private GameSpots Spots()
    {
        var spots = new GameSpots(folder.Data);
        spots.Load();
        return spots;
    }

    [Fact]
    public void ChangesOnlyInTheKnownSpotSuggestNothing()
    {
        DateTime start = DateTime.UtcNow.AddMinutes(-1);
        TestFolder.Write(Path.Combine(folder.Game, "slot.sav"), "x");

        var spots = Spots();
        spots.Add(folder.Game, "", folder.Game, SaveSource.Userdata);

        Assert.Empty(Run(spots, start));
    }

    [Fact]
    public void AFolderWithTheGameNameInDocumentsIsSuggested()
    {
        DateTime start = DateTime.UtcNow.AddMinutes(-1);
        TestFolder.Write(Path.Combine(Documents, "Hollow Knight", "slot1.sav"), "x");
        TestFolder.Write(Path.Combine(Documents, "Hollow Knight", "slot2.sav"), "y");

        IReadOnlyList<SpotSuggestion> found = Run(Spots(), start);

        SpotSuggestion suggestion = Assert.Single(found);
        Assert.Equal(Path.Combine(Documents, "Hollow Knight"), suggestion.Folder);
        Assert.True(suggestion.Score >= SessionScan.MinScore);
        Assert.NotEmpty(suggestion.Reasons);
    }

    [Fact]
    public void AFolderIgnoredForTheGameIsNotSuggested()
    {
        DateTime start = DateTime.UtcNow.AddMinutes(-1);
        string ignored = Path.Combine(Documents, "Hollow Knight");
        TestFolder.Write(Path.Combine(ignored, "slot1.sav"), "x");
        TestFolder.Write(Path.Combine(ignored, "slot2.sav"), "y");
        new IgnoredFolders(Lss).AddForGame(folder.Data, ignored);

        Assert.Empty(Run(Spots(), start));
    }

    [Fact]
    public void ACacheFolderIsNotSuggested()
    {
        DateTime start = DateTime.UtcNow.AddMinutes(-1);
        TestFolder.Write(Path.Combine(Documents, "Cache", "a1.dat"), "x");
        TestFolder.Write(Path.Combine(Documents, "Cache", "a2.dat"), "y");

        Assert.Empty(Run(Spots(), start));
    }

    [Fact]
    public void LooseSaveFilesInTheInstallFolderBecomeAPattern()
    {
        DateTime start = DateTime.UtcNow.AddMinutes(-1);
        TestFolder.Write(Path.Combine(Install, "slot1.sav"), "x");
        TestFolder.Write(Path.Combine(Install, "slot2.sav"), "y");
        TestFolder.Write(Path.Combine(Install, "slot3.sav"), "z");

        IReadOnlyList<SpotSuggestion> found = Run(Spots(), start);

        SpotSuggestion suggestion = Assert.Single(found, s => s.Pattern != "");
        Assert.Equal(Install, suggestion.Folder);
        Assert.Equal("*.sav", suggestion.Pattern);
    }

    // "Non sono salvataggi" su file sparsi: scarta la proposta, non la cartella del gioco.
    [Fact]
    public void ADismissedLooseFileSuggestionDoesNotComeBackButTheFolderIsStillSearched()
    {
        DateTime start = DateTime.UtcNow.AddMinutes(-1);
        TestFolder.Write(Path.Combine(Install, "slot1.sav"), "x");
        TestFolder.Write(Path.Combine(Install, "slot2.sav"), "y");
        TestFolder.Write(Path.Combine(Install, "slot3.sav"), "z");
        Run(Spots(), start);

        SessionScan.Dismiss(folder.Data, Install, "*.sav");

        Assert.Empty(SessionScan.Pending(folder.Data));
        Assert.DoesNotContain(Run(Spots(), start), s => s.Pattern == "*.sav");
        Assert.Empty(new IgnoredFolders(Lss).ForGame(folder.Data));

        TestFolder.Write(Path.Combine(Install, "a.dat"), "1");
        Assert.Contains(Run(Spots(), start), s => s.Folder == Install && s.Pattern == "a.dat");
    }

    [Fact]
    public void SuggestionsStayUntilForgotten()
    {
        DateTime start = DateTime.UtcNow.AddMinutes(-1);
        TestFolder.Write(Path.Combine(Documents, "Hollow Knight", "slot1.sav"), "x");
        TestFolder.Write(Path.Combine(Documents, "Hollow Knight", "slot2.sav"), "y");
        Run(Spots(), start);

        Assert.Single(SessionScan.Pending(folder.Data));
        Assert.True(SessionScan.Forget(folder.Data, Path.Combine(Documents, "Hollow Knight"), ""));
        Assert.Empty(SessionScan.Pending(folder.Data));
    }

    [Fact]
    public void SavedSuggestionsKeepTheirReasons()
    {
        DateTime start = DateTime.UtcNow.AddMinutes(-1);
        TestFolder.Write(Path.Combine(Documents, "Hollow Knight", "slot1.sav"), "x");
        TestFolder.Write(Path.Combine(Documents, "Hollow Knight", "slot2.sav"), "y");
        SpotSuggestion found = Assert.Single(Run(Spots(), start));

        SpotSuggestion saved = Assert.Single(SessionScan.Pending(folder.Data));

        Assert.NotEmpty(found.Reasons);
        Assert.Equal(found.Reasons.Select(reason => (reason.Kind, reason.Points, reason.Detail)),
            saved.Reasons.Select(reason => (reason.Kind, reason.Points, reason.Detail)));
    }

    // R11: lo stesso percorso scritto con altri separatori e uno finale (come una build vecchia).
    private static string Other(string path) =>
        path.Replace(Path.DirectorySeparatorChar, Path.DirectorySeparatorChar == '/' ? '\\' : '/') + "/";

    private string Changed()
    {
        string dir = Path.Combine(Documents, "Hollow Knight");
        TestFolder.Write(Path.Combine(dir, "slot1.sav"), "x");
        TestFolder.Write(Path.Combine(dir, "slot2.sav"), "y");
        return dir;
    }

    [Fact]
    public void AKnownSpotWithOtherSeparatorsIsNotSuggestedAgain()
    {
        DateTime start = DateTime.UtcNow.AddMinutes(-1);
        string dir = Changed();
        var spots = Spots();
        spots.Add(Other(dir), "", Other(dir), SaveSource.Chosen);

        Assert.Empty(Run(spots, start));
    }

    [Fact]
    public void ADismissedSuggestionWithOtherSeparatorsIsNotSuggestedAgain()
    {
        DateTime start = DateTime.UtcNow.AddMinutes(-1);
        string dir = Changed();
        SessionScan.Dismiss(folder.Data, Other(dir), "");

        Assert.Empty(Run(Spots(), start));
    }

    [Fact]
    public void AStoredSuggestionForAKnownSpotGoesAway()
    {
        DateTime start = DateTime.UtcNow.AddMinutes(-1);
        string dir = Changed();
        JsonFile.Write(Path.Combine(folder.Data, "proposte.json"), new SuggestionsFile
        {
            Suggestions = [new SuggestionEntry { Folder = Other(dir), Score = 9, Found = DateTime.UtcNow.ToString("O") }],
        });
        var spots = Spots();
        spots.Add(dir, "", dir, SaveSource.Chosen);

        Assert.Empty(Run(spots, start));
        Assert.Empty(SessionScan.Pending(folder.Data));
    }
}
