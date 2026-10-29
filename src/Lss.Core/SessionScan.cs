using System.Globalization;
using System.Text.Json.Serialization;

namespace Lss;

/// <summary>Cartella che sembra di salvataggi, trovata dopo una partita e non ancora un posto.</summary>
/// <param name="Folder">Cartella (per i file sparsi nella cartella del gioco, la cartella del gioco).</param>
/// <param name="Pattern">"" per una cartella intera, o le maschere dei file.</param>
/// <param name="Score">Punteggio (per i file sparsi, la soglia minima).</param>
/// <param name="Reasons">Perché: restano in proposte.json insieme alla proposta, così la finestra li mostra.</param>
/// <param name="FoundUtc">Quando è stata trovata.</param>
public sealed record SpotSuggestion(string Folder, string Pattern, int Score, IReadOnlyList<ScoreReason> Reasons,
    DateTime FoundUtc);

/// <summary>Un motivo di una proposta, come è scritto in proposte.json.</summary>
internal sealed class ReasonEntry
{
    [JsonPropertyName("Tipo")]
    public string Kind { get; set; } = "";

    [JsonPropertyName("Punti")]
    public int Points { get; set; }

    [JsonPropertyName("Dettaglio")]
    public string Detail { get; set; } = "";
}

/// <summary>Una proposta in attesa, come è scritta in proposte.json.</summary>
internal sealed class SuggestionEntry
{
    [JsonPropertyName("Cartella")]
    public string Folder { get; set; } = "";

    [JsonPropertyName("Maschera")]
    public string Pattern { get; set; } = "";

    [JsonPropertyName("Punteggio")]
    public int Score { get; set; }

    // Data in formato "O" (ISO 8601, UTC).
    [JsonPropertyName("Trovata")]
    public string Found { get; set; } = "";

    [JsonPropertyName("Motivi")]
    public List<ReasonEntry> Reasons { get; set; } = [];
}

/// <summary>Una proposta di file sparsi scartata, come è scritta in proposte.json.</summary>
internal sealed class DismissedEntry
{
    [JsonPropertyName("Cartella")]
    public string Folder { get; set; } = "";

    [JsonPropertyName("Maschera")]
    public string Pattern { get; set; } = "";
}

/// <summary>Come proposte.json è scritto: le proposte in attesa e quelle scartate.</summary>
internal sealed class SuggestionsFile
{
    [JsonPropertyName("Proposte")]
    public List<SuggestionEntry> Suggestions { get; set; } = [];

    // Proposte di file sparsi scartate ("Non sono salvataggi"): cartella e maschera. Non tornano.
    [JsonPropertyName("Scartate")]
    public List<DismissedEntry> Dismissed { get; set; } = [];
}

/// <summary>
/// Guarda, dopo una partita, le cartelle e i file cambiati sul disco e propone quelli
/// che sembrano salvataggi e non sono ancora un posto del gioco (N10). Legge soltanto,
/// e salva le proposte in proposte.json finché non si accettano o non si ignorano.
/// </summary>
internal static class SessionScan
{
    /// <summary>Più severo della ricerca della fase M: nessuno l'ha chiesto, meglio pochi falsi positivi.</summary>
    public const int MinScore = 9;

    private const string FileName = "proposte.json";
    private const int LooseGroupMin = 3;
    private static readonly TimeSpan TimeLimit = TimeSpan.FromSeconds(90);
    private static readonly string[] SaveExtensions = [".sav", ".save", ".dat", ".sl2", ".slot"];

    /// <summary>
    /// Le proposte in attesa per il gioco, dopo la ricerca sulle cartelle cambiate da
    /// sessionStartUtc (con 5 secondi di margine). Le nuove si aggiungono alle salvate.
    /// </summary>
    public static IReadOnlyList<SpotSuggestion> Run(GameInfo game, SaveContext context, DateTime sessionStartUtc,
        GameSpots spots, IReadOnlyList<string> launcherFolders, string lssDataRoot, CancellationToken cancel)
    {
        string dataFolder = spots.GameDataFolder;
        IReadOnlyList<string> ignoredFolders = new IgnoredFolders(lssDataRoot).Folders(dataFolder, context);
        string userdata = game.Source == SaveSource.Userdata ? game.SaveFolder : "";

        var skip = new List<string>(ignoredFolders) { lssDataRoot };
        skip.AddRange(launcherFolders.Where(folder => !Same(folder, userdata) && !Same(folder, game.InstallPath)));

        IReadOnlyList<SearchRoot> roots = SaveFinder.Roots(context, spots.ExtraRoots, userdata);
        IReadOnlyList<ChangedFile> files = SaveFinder.FindChanged(roots, sessionStartUtc.AddSeconds(-5), skip,
            TimeLimit, cancel, out _);

        IReadOnlyList<string> known = game.Spots.Select(spot => spot.Folder)
            .Concat(spots.Spots.Select(spot => spot.Folder))
            .Where(folder => folder != "")
            .ToList();

        IReadOnlyList<SaveCandidate> candidates = SaveFinder.Rank(files, context, game.Name, launcherFolders,
            lssDataRoot, known, ignoredFolders);

        var fresh = candidates
            .Where(candidate => candidate.Score >= MinScore)
            .Select(candidate => new SpotSuggestion(candidate.Folder, "", candidate.Score, candidate.Reasons, DateTime.UtcNow))
            .ToList();

        fresh.AddRange(LooseSuggestions(game, context, launcherFolders, lssDataRoot, files));

        var pending = Pending(dataFolder).ToList();
        IReadOnlyList<(string Folder, string Pattern)> dismissed = Dismissed(dataFolder);

        // Una proposta già salvata che intanto è diventata un posto non serve più.
        pending.RemoveAll(other => other.Pattern == "" && known.Any(folder => Same(folder, other.Folder)));

        foreach (SpotSuggestion suggestion in fresh)
        {
            bool already = pending.Any(other => Same(other.Folder, suggestion.Folder) && other.Pattern == suggestion.Pattern);
            bool isSpot = known.Any(folder => Same(folder, suggestion.Folder) && suggestion.Pattern == "");
            bool isDismissed = dismissed.Any(item => Same(item.Folder, suggestion.Folder) && item.Pattern == suggestion.Pattern);

            if (!already && !isSpot && !isDismissed)
                pending.Add(suggestion);
        }

        Store(dataFolder, pending, dismissed);

        return pending;
    }

    /// <summary>
    /// Toglie una proposta (accettata o ignorata). Ritorna true se c'era.
    /// </summary>
    public static bool Forget(string gameDataFolder, string folder, string pattern)
    {
        var pending = Pending(gameDataFolder).ToList();
        int removed = pending.RemoveAll(suggestion => Same(suggestion.Folder, folder) && suggestion.Pattern == pattern);

        if (removed > 0)
            Store(gameDataFolder, pending, Dismissed(gameDataFolder));

        return removed > 0;
    }

    /// <summary>
    /// Scarta una proposta di file sparsi per questo gioco: non torna più. La cartella
    /// (quella del gioco) non viene ignorata, così altre proposte lì restano possibili.
    /// </summary>
    public static void Dismiss(string gameDataFolder, string folder, string pattern)
    {
        var pending = Pending(gameDataFolder).ToList();
        pending.RemoveAll(suggestion => Same(suggestion.Folder, folder) && suggestion.Pattern == pattern);

        var dismissed = Dismissed(gameDataFolder).ToList();

        if (!dismissed.Any(item => Same(item.Folder, folder) && item.Pattern == pattern))
            dismissed.Add((folder, pattern));

        Store(gameDataFolder, pending, dismissed);
    }

    /// <summary>Le proposte di file sparsi scartate, come cartella e maschera.</summary>
    public static IReadOnlyList<(string Folder, string Pattern)> Dismissed(string gameDataFolder)
    {
        SuggestionsFile? file = JsonFile.Read<SuggestionsFile>(Path.Combine(gameDataFolder, FileName));

        return file?.Dismissed
            .Where(item => item.Folder != "")
            .Select(item => (item.Folder, item.Pattern))
            .ToList() ?? [];
    }

    /// <summary>
    /// Le proposte salvate in proposte.json, con i loro motivi. Una proposta senza
    /// motivi resta valida.
    /// </summary>
    public static IReadOnlyList<SpotSuggestion> Pending(string gameDataFolder)
    {
        var result = new List<SpotSuggestion>();
        SuggestionsFile? file = JsonFile.Read<SuggestionsFile>(Path.Combine(gameDataFolder, FileName));

        foreach (SuggestionEntry entry in file?.Suggestions ?? [])
        {
            if (entry.Folder == "")
                continue;

            DateTime.TryParse(entry.Found, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime found);

            // I motivi con un tipo che non si conosce si saltano.
            var reasons = entry.Reasons
                .Where(reason => Enum.TryParse(reason.Kind, out ScoreReasonKind _))
                .Select(reason => new ScoreReason(Enum.Parse<ScoreReasonKind>(reason.Kind), reason.Points, reason.Detail))
                .ToList();

            result.Add(new SpotSuggestion(entry.Folder, entry.Pattern, entry.Score, reasons, found));
        }

        return result;
    }

    private static void Store(string gameDataFolder, IReadOnlyList<SpotSuggestion> suggestions,
        IReadOnlyList<(string Folder, string Pattern)> dismissed)
    {
        string path = Path.Combine(gameDataFolder, FileName);

        try
        {
            if (suggestions.Count == 0 && dismissed.Count == 0)
            {
                if (File.Exists(path))
                    File.Delete(path);

                return;
            }

            JsonFile.Write(path, new SuggestionsFile
            {
                Suggestions = suggestions.Select(suggestion => new SuggestionEntry
                {
                    Folder = suggestion.Folder,
                    Pattern = suggestion.Pattern,
                    Score = suggestion.Score,
                    Found = suggestion.FoundUtc.ToString("O", CultureInfo.InvariantCulture),
                    Reasons = suggestion.Reasons.Select(reason => new ReasonEntry
                    {
                        Kind = reason.Kind.ToString(),
                        Points = reason.Points,
                        Detail = reason.Detail,
                    }).ToList(),
                }).ToList(),
                Dismissed = dismissed.Select(item => new DismissedEntry { Folder = item.Folder, Pattern = item.Pattern }).ToList(),
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Write("proposte.json non salvato: " + path, ex);
        }
    }

    /// <summary>
    /// I file sparsi che sembrano salvataggi nella cartella di installazione: una maschera
    /// per estensione se ce ne sono almeno tre, altrimenti i nomi esatti. Deve passare il controllo.
    /// </summary>
    private static IEnumerable<SpotSuggestion> LooseSuggestions(GameInfo game, SaveContext context,
        IReadOnlyList<string> launchers, string lssDataRoot, IReadOnlyList<ChangedFile> files)
    {
        if (game.InstallPath == "")
            yield break;

        var loose = files
            .Where(file => Same(Path.GetDirectoryName(file.Path) ?? "", game.InstallPath))
            .Where(file => SaveExtensions.Contains(Path.GetExtension(file.Path).ToLowerInvariant()))
            .GroupBy(file => Path.GetExtension(file.Path).ToLowerInvariant());

        foreach (var group in loose)
        {
            string pattern = group.Count() >= LooseGroupMin
                ? "*" + group.Key
                : string.Join(";", group.Select(file => Path.GetFileName(file.Path)));

            if (SaveFolderSafety.CheckPattern(game.InstallPath, pattern, false, context, launchers, lssDataRoot)
                != SafetyProblem.None)
            {
                continue;
            }

            yield return new SpotSuggestion(game.InstallPath, pattern, MinScore,
                [new ScoreReason(ScoreReasonKind.SaveLikeFile, 0, group.Key)], DateTime.UtcNow);
        }
    }

    // Percorsi scritti con separatori diversi (R11) sono la stessa cartella.
    private static bool Same(string first, string second) => PathText.Same(first, second);
}
