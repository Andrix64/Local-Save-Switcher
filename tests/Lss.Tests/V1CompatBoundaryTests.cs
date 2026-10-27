namespace Lss.Tests;

/// <summary>
/// I nomi dei file della 1.0 non devono comparire fuori da V1Compat.cs e
/// LegacyMigration.cs: così il codice nuovo non rimette in giro i vecchi formati.
/// Le eccezioni di oggi sono elencate una per una e le toglie la fase R (R10).
/// </summary>
public sealed class V1CompatBoundaryTests
{
    private static readonly string[] OldNames =
    [
        "lss.ini", "persone.ini", "preferite.ini", "stato.ini", "posti.ini", "cartella.ini",
        "proposte.ini", "cartelle-ignorate.ini", "gioco.ini", "posizioni.ini", "partita.ini"
    ];

    // File che possono nominare i vecchi file: la compatibilità e Control Save Manager.
    private static readonly string[] AllowedFiles = ["V1Compat.cs", "LegacyMigration.cs"];

    // Eccezioni di oggi: file -> nomi. Ogni riga dice quale task la toglie.
    private static readonly Dictionary<string, string[]> Exceptions = new()
    {
        ["SaveManager.cs"] = ["stato.ini"], // fino a R10
        ["IgnoredFolders.cs"] = ["posti.ini"], // fino a R10
        ["MainWindowViewModel.cs"] = ["stato.ini"], // fino a R10
        ["GameSpots.cs"] = ["posti.ini"], // fino a R10
        ["SaveManager.Favourites.cs"] = ["preferite.ini"], // fino a R10
        ["SessionScan.cs"] = ["proposte.ini"], // fino a R10
        ["SaveManager.Playthroughs.cs"] = ["partita.ini", "posizioni.ini"], // fino a R10
    };

    private static string RepositoryRoot()
    {
        string? dir = AppContext.BaseDirectory;

        while (dir != null && !File.Exists(Path.Combine(dir, "LSS.slnx")))
            dir = Path.GetDirectoryName(dir);

        return dir ?? throw new InvalidOperationException("LSS.slnx non trovato salendo da " + AppContext.BaseDirectory);
    }

    [Fact]
    public void OldFileNamesAppearOnlyInV1CompatAndLegacyMigration()
    {
        string src = Path.Combine(RepositoryRoot(), "src");
        var found = new List<string>();

        foreach (string file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(src, file);

            if (relative.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
                continue;

            string name = Path.GetFileName(file);

            if (AllowedFiles.Contains(name))
                continue;

            string text = File.ReadAllText(file);

            foreach (string old in OldNames.Where(old => text.Contains("\"" + old + "\"")))
            {
                if (!Exceptions.TryGetValue(name, out string[]? allowed) || !allowed.Contains(old))
                    found.Add(relative + ": " + old);
            }
        }

        Assert.True(found.Count == 0, "Vecchi nomi di file fuori da V1Compat: " + string.Join("; ", found));
    }

    [Fact]
    public void EveryExceptionIsStillNeeded()
    {
        string src = Path.Combine(RepositoryRoot(), "src");

        foreach ((string name, string[] olds) in Exceptions)
        {
            string file = Directory.EnumerateFiles(src, name, SearchOption.AllDirectories)
                .First(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar));
            string text = File.ReadAllText(file);

            foreach (string old in olds)
                Assert.True(text.Contains("\"" + old + "\""), $"{name} non usa più {old}: togli l'eccezione");
        }
    }
}
