using System.Globalization;

namespace Lss;

/// <summary>Cartella che sembra di salvataggi, trovata dopo una partita e non ancora un posto.</summary>
/// <param name="Folder">Cartella (per i file sparsi nella cartella del gioco, la cartella del gioco).</param>
/// <param name="Pattern">"" per una cartella intera, o le maschere dei file.</param>
/// <param name="Score">Punteggio (per i file sparsi, la soglia minima).</param>
/// <param name="Reasons">Perché: restano in proposte.ini insieme alla proposta, così la finestra li mostra.</param>
/// <param name="FoundUtc">Quando è stata trovata.</param>
public sealed record SpotSuggestion(string Folder, string Pattern, int Score, IReadOnlyList<ScoreReason> Reasons,
    DateTime FoundUtc);

/// <summary>
/// Guarda, dopo una partita, le cartelle e i file cambiati sul disco e propone quelli
/// che sembrano salvataggi e non sono ancora un posto del gioco (N10). Legge soltanto,
/// e salva le proposte in proposte.ini finché non si accettano o non si ignorano.
/// </summary>
internal static class SessionScan
{
    /// <summary>Più severo della ricerca della fase M: nessuno l'ha chiesto, meglio pochi falsi positivi.</summary>
    public const int MinScore = 9;

    private const string FileName = "proposte.ini";
    private const string Key = "Proposta.";
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

        foreach (SpotSuggestion suggestion in fresh)
        {
            bool already = pending.Any(other => Same(other.Folder, suggestion.Folder) && other.Pattern == suggestion.Pattern);
            bool isSpot = known.Any(folder => Same(folder, suggestion.Folder) && suggestion.Pattern == "");

            if (!already && !isSpot)
                pending.Add(suggestion);
        }

        Store(dataFolder, pending);

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
            Store(gameDataFolder, pending);

        return removed > 0;
    }

    /// <summary>
    /// Le proposte salvate in proposte.ini, con i loro motivi. Una riga senza motivi
    /// (com'era prima) resta valida.
    /// </summary>
    public static IReadOnlyList<SpotSuggestion> Pending(string gameDataFolder)
    {
        var result = new List<SpotSuggestion>();

        foreach ((string name, string value) in IniFile.Read(Path.Combine(gameDataFolder, FileName)))
        {
            if (!name.StartsWith(Key, StringComparison.OrdinalIgnoreCase))
                continue;

            string[] parts = value.Split('|');

            if (parts.Length is < 4 or > 5 || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int score))
                continue;

            DateTime.TryParse(parts[3], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime found);
            IReadOnlyList<ScoreReason> reasons = parts.Length == 5 ? ReadReasons(parts[4]) : [];
            result.Add(new SpotSuggestion(parts[0], parts[1], score, reasons, found));
        }

        return result;
    }

    /// <summary>I motivi salvati, come "tipo:punti:dettaglio" separati da ";". Quelli non letti si saltano.</summary>
    private static IReadOnlyList<ScoreReason> ReadReasons(string text)
    {
        var reasons = new List<ScoreReason>();

        foreach (string item in text.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] fields = item.Split(':', 3);

            if (fields.Length == 3
                && Enum.TryParse(fields[0], out ScoreReasonKind kind)
                && int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int points))
            {
                reasons.Add(new ScoreReason(kind, points, fields[2]));
            }
        }

        return reasons;
    }

    /// <summary>I motivi da salvare: "|" e ";" nel dettaglio diventano spazi, per non rompere la riga.</summary>
    private static string WriteReasons(IReadOnlyList<ScoreReason> reasons) =>
        string.Join(";", reasons.Select(reason => string.Join(":", reason.Kind.ToString(),
            reason.Points.ToString(CultureInfo.InvariantCulture), reason.Detail.Replace('|', ' ').Replace(';', ' '))));

    private static void Store(string gameDataFolder, IReadOnlyList<SpotSuggestion> suggestions)
    {
        string path = Path.Combine(gameDataFolder, FileName);

        try
        {
            if (suggestions.Count == 0)
            {
                if (File.Exists(path))
                    File.Delete(path);

                return;
            }

            var values = suggestions.Select((suggestion, index) => new KeyValuePair<string, string>(
                Key + index.ToString(CultureInfo.InvariantCulture),
                string.Join("|", suggestion.Folder, suggestion.Pattern,
                    suggestion.Score.ToString(CultureInfo.InvariantCulture),
                    suggestion.FoundUtc.ToString("O", CultureInfo.InvariantCulture),
                    WriteReasons(suggestion.Reasons))));

            IniFile.Write(path, values);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Write("proposte.ini non salvato: " + path, ex);
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

    private static bool Same(string first, string second) =>
        string.Equals(first, second, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
