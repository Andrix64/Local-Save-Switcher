using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Lss;

/// <summary>Cartella in cui cercare i file cambiati, con la profondità massima.</summary>
/// <param name="Folder">Cartella da leggere.</param>
/// <param name="MaxDepth">Livelli di sottocartelle da leggere sotto Folder.</param>
/// <param name="GameOwned">true se tutto quello che sta dentro appartiene a questo gioco.</param>
public sealed record SearchRoot(string Folder, int MaxDepth, bool GameOwned);

/// <summary>File cambiato dopo l'avvio del gioco.</summary>
/// <param name="Path">Percorso completo del file.</param>
/// <param name="Size">Dimensione in byte.</param>
/// <param name="WrittenUtc">Ultima scrittura (UTC).</param>
/// <param name="Root">Cartella di ricerca in cui è stato trovato.</param>
public sealed record ChangedFile(string Path, long Size, DateTime WrittenUtc, SearchRoot Root);

/// <summary>Perché una cartella ha preso (o perso) punti: mostrato all'utente.</summary>
public enum ScoreReasonKind { GameName, GameOwned, SaveLikeFile, ChangedFiles, SaveLikePath, OnlySettings, OtherProgram }

/// <summary>Una voce del punteggio, con il dettaglio da mostrare (nome trovato, file, numero).</summary>
/// <param name="Kind">Che cosa ha dato o tolto punti.</param>
/// <param name="Points">Punti: positivi o negativi.</param>
/// <param name="Detail">Nome della cartella, nome del file o numero, secondo il tipo.</param>
public sealed record ScoreReason(ScoreReasonKind Kind, int Points, string Detail);

/// <summary>Cartella proposta come cartella dei salvataggi.</summary>
/// <param name="Folder">Cartella da sostituire per intero cambiando partita.</param>
/// <param name="Score">Somma dei punti dei motivi.</param>
/// <param name="Files">File cambiati dentro la cartella.</param>
/// <param name="Reasons">Perché ha preso (o perso) punti.</param>
public sealed record SaveCandidate(string Folder, int Score, IReadOnlyList<ChangedFile> Files,
    IReadOnlyList<ScoreReason> Reasons);

/// <summary>
/// Trova la cartella dei salvataggi di un gioco guardando i file cambiati
/// sul disco mentre il gioco era aperto (fase M). Legge soltanto.
/// </summary>
internal static partial class SaveFinder
{
    // Profondità per le cartelle che sono del gioco: installazione, userdata, prefisso.
    private const int GameDepth = 8;

    // Pesi del punteggio (Rank): da tarare con i test reali, fase M7.
    private const int GameNamePoints = 6;
    private const int GameOwnedPoints = 5;
    private const int SaveLikeFilePoints = 3;
    private const int ChangedFilePoints = 1;
    private const int ChangedFilesMaxPoints = 3;
    private const int SaveWordPoints = 2;
    private const int SaveWordsPoints = 3;
    private const int SettingsOnlyPoints = -4;
    private const int OtherProgramPoints = -6;
    private const int MinScore = 3;
    private const int MaxCandidates = 5;
    private const int MinTokenLength = 3;

    private static readonly HashSet<string> SaveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".sav", ".save", ".sl2", ".ess", ".dat", ".bin", ".slot"
    };

    private static readonly string[] SaveWords = ["save", "slot", "profile", "progress", "player", "world"];

    private static readonly HashSet<string> SettingsExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ini", ".cfg", ".xml", ".json"
    };

    private static readonly string[] SettingsWords = ["config", "setting", "option", "prefs"];

    // Nomi tipici di una cartella di salvataggi, dopo Normalize.
    private static readonly HashSet<string> SaveFolderWords =
    [
        "saves", "save", "savegames", "savedgames", "saved", "savedata", "savefiles", "profiles", "mygames"
    ];

    /// <summary>
    /// Cartelle in cui un gioco può salvare, senza doppioni né cartelle una dentro
    /// l'altra. userdataFolder è userdata/account/AppID di Steam, o "".
    /// </summary>
    public static IReadOnlyList<SearchRoot> Roots(SaveContext context, IReadOnlyList<string> extraFolders,
        string userdataFolder = "")
    {
        KnownFolders folders = context.Folders;
        var found = new List<SearchRoot>();

        Add(found, context.InstallPath, GameDepth, true);
        Add(found, userdataFolder, GameDepth, true);

        foreach (string extra in extraFolders)
            Add(found, extra, GameDepth, false);

        // Cartelle dell'utente di questo sistema (su Linux solo Documents e XDG).
        Add(found, folders.Documents, 6, false);
        Add(found, folders.SavedGames, 6, false);
        Add(found, folders.AppData, 6, false);
        Add(found, folders.LocalAppData, 6, false);
        Add(found, folders.LocalAppDataLow, 6, false);
        Add(found, folders.XdgData, 5, false);
        Add(found, folders.XdgConfig, 5, false);

        if (folders.Public != "")
            Add(found, Path.Combine(folders.Public, "Documents"), 4, false);

        Add(found, folders.ProgramData, 4, false);

        // Su Linux: le cartelle nascoste direttamente in Home (~/.mygame).
        if (!OperatingSystem.IsWindows() && folders.Home != "")
        {
            foreach (string child in Children(folders.Home))
            {
                if (Path.GetFileName(child).StartsWith('.'))
                    Add(found, child, 4, false);
            }
        }

        if (context.PrefixPath != "")
            AddPrefix(found, context);
        else if (folders.Home != "")
            AddWithoutPrefix(found, folders.Home);

        return Merge(found, Comparison(context));
    }

    /// <summary>
    /// Cartelle di un prefisso Proton o Wine: la cartella dell'utente (che è del
    /// gioco se il prefisso è di un solo gioco) e le cartelle comuni di Windows.
    /// </summary>
    private static void AddPrefix(List<SearchRoot> found, SaveContext context)
    {
        bool owned = context.PrefixIsPerGame;
        string user = SavePathResolver.PrefixUser(context);

        Add(found, user, 7, owned);
        Add(found, Path.Combine(context.PrefixPath, "drive_c", "ProgramData"), 5, owned);
        Add(found, Path.Combine(context.PrefixPath, "drive_c", "users", "Public"), 5, owned);
    }

    /// <summary>
    /// Prefissi Wine senza un gioco preciso: quelli più comuni su Linux (Wine
    /// diretto, Games, Bottles nativo e Flatpak).
    /// </summary>
    private static void AddWithoutPrefix(List<SearchRoot> found, string home)
    {
        Add(found, Path.Combine(home, ".wine", "drive_c", "users"), 6, false);

        foreach (string game in Children(Path.Combine(home, "Games")))
            Add(found, Path.Combine(game, "drive_c", "users"), 6, false);

        foreach (string bottle in Children(Path.Combine(home, ".local", "share", "bottles", "bottles")))
            Add(found, Path.Combine(bottle, "drive_c", "users"), 6, false);

        string flatpak = Path.Combine(home, ".var", "app", "com.usebottles.bottles", "data", "bottles", "bottles");

        foreach (string bottle in Children(flatpak))
            Add(found, Path.Combine(bottle, "drive_c", "users"), 6, false);
    }

    /// <summary>
    /// Aggiunge la cartella se esiste, con la sua profondità e il suo proprietario.
    /// </summary>
    private static void Add(List<SearchRoot> found, string folder, int maxDepth, bool gameOwned)
    {
        if (folder == "" || !Directory.Exists(folder))
            return;

        found.Add(new SearchRoot(Full(folder), maxDepth, gameOwned));
    }

    /// <summary>
    /// Unisce le cartelle una dentro l'altra: resta quella esterna, con la
    /// profondità maggiore. Una cartella del gioco dentro una cartella non
    /// del gioco resta a parte, così il punteggio la riconosce.
    /// </summary>
    private static IReadOnlyList<SearchRoot> Merge(List<SearchRoot> found, StringComparison comparison)
    {
        var result = new List<SearchRoot>();

        foreach (SearchRoot root in found.OrderBy(r => r.Folder.Length).ThenByDescending(r => r.GameOwned))
        {
            int same = result.FindIndex(r => string.Equals(r.Folder, root.Folder, comparison));

            if (same >= 0)
            {
                SearchRoot kept = result[same];
                result[same] = kept with { MaxDepth = Math.Max(kept.MaxDepth, root.MaxDepth), GameOwned = kept.GameOwned || root.GameOwned };
                continue;
            }

            int outer = result.FindIndex(r => IsInside(root.Folder, r.Folder, comparison)
                && (!root.GameOwned || r.GameOwned));

            if (outer >= 0)
            {
                SearchRoot kept = result[outer];
                result[outer] = kept with { MaxDepth = Math.Max(kept.MaxDepth, root.MaxDepth) };
                continue;
            }

            result.Add(root);
        }

        return result
            .OrderByDescending(r => r.GameOwned)
            .ThenBy(r => r.Folder, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// true se path sta dentro folder (non è la stessa cartella).
    /// </summary>
    private static bool IsInside(string path, string folder, StringComparison comparison) =>
        path.StartsWith(folder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, comparison);

    /// <summary>
    /// Windows non distingue maiuscole e minuscole, e nel prefisso i nomi possono
    /// cambiare caso: in quei casi il confronto ignora le maiuscole.
    /// </summary>
    private static StringComparison Comparison(SaveContext context) =>
        context.IgnoreCase || OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    /// <summary>
    /// Percorso assoluto senza separatore finale.
    /// </summary>
    private static string Full(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    /// <summary>
    /// Cartelle proposte come cartella dei salvataggi, in ordine di punteggio.
    /// knownFolders sono le cartelle che il gioco ha già: i file dentro non si
    /// propongono di nuovo. ignoredFolders sono le cartelle segnate come "non di
    /// salvataggi" (IgnoredFolders.Folders): i file dentro si tolgono. Le cartelle
    /// che non passano SaveFolderSafety, o con meno di MinScore punti, non compaiono.
    /// </summary>
    public static IReadOnlyList<SaveCandidate> Rank(IReadOnlyList<ChangedFile> files, SaveContext context,
        string gameName, IReadOnlyList<string> launcherFolders, string lssDataRoot,
        IReadOnlyList<string> knownFolders, IReadOnlyList<string>? ignoredFolders = null)
    {
        StringComparison comparison = Comparison(context);
        IReadOnlyList<string> tokens = GameTokens(gameName, Path.GetFileName(context.InstallPath));
        IReadOnlyList<string> ignored = ignoredFolders ?? [];
        IReadOnlyList<ChangedFile> kept = files
            .Where(file => !ignored.Any(folder => string.Equals(file.Path, folder, comparison) || IsInside(file.Path, folder, comparison)))
            .ToList();
        var candidates = new List<SaveCandidate>();

        foreach (Proposal proposal in Propose(kept, knownFolders, tokens, comparison))
        {
            if (SaveFolderSafety.Check(proposal.Folder, context, launcherFolders, lssDataRoot) != SafetyProblem.None)
                continue;

            SaveCandidate candidate = Score(proposal, tokens, comparison);

            if (candidate.Score >= MinScore)
                candidates.Add(candidate);
        }

        return candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.Files.Max(file => file.WrittenUtc))
            .Take(MaxCandidates)
            .ToList();
    }

    /// <summary>
    /// Minuscole, e solo lettere e cifre: "Hollow Knight™" diventa "hollowknight".
    /// </summary>
    internal static string Normalize(string text) => NotLetterOrDigit().Replace(text.ToLowerInvariant(), "");

    [GeneratedRegex("[^\\p{L}\\p{N}]+")]
    private static partial Regex NotLetterOrDigit();

    /// <summary>
    /// Parole del nome del gioco e della cartella di installazione, di almeno tre
    /// caratteri, più il nome intero normalizzato.
    /// </summary>
    internal static IReadOnlyList<string> GameTokens(string gameName, string installDir)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);

        foreach (string text in new[] { gameName, installDir })
        {
            string whole = Normalize(text);

            if (whole.Length >= MinTokenLength)
                tokens.Add(whole);

            foreach (string word in NotLetterOrDigit().Split(text.ToLowerInvariant()))
            {
                if (word.Length >= MinTokenLength)
                    tokens.Add(word);
            }
        }

        return tokens.ToList();
    }

    /// <summary>
    /// true se il nome contiene una parola del gioco (dopo Normalize).
    /// </summary>
    private static bool MatchesToken(string name, IReadOnlyList<string> tokens)
    {
        string normal = Normalize(name);

        return tokens.Any(token => normal.Contains(token, StringComparison.Ordinal));
    }

    /// <summary>
    /// Cartella proposta: i file cambiati dentro, e la radice in cui sono stati trovati.
    /// </summary>
    private sealed record Proposal(string Folder, SearchRoot Root, List<ChangedFile> Files);

    /// <summary>
    /// Raggruppa i file per cartella madre, la alza alla cartella del gioco, e
    /// unisce quelle una dentro l'altra.
    /// </summary>
    private static IReadOnlyList<Proposal> Propose(IReadOnlyList<ChangedFile> files,
        IReadOnlyList<string> knownFolders, IReadOnlyList<string> tokens, StringComparison comparison)
    {
        var raised = new List<Proposal>();

        IEnumerable<ChangedFile> fresh = files.Where(file =>
            !knownFolders.Any(known => string.Equals(file.Path, known, comparison) || IsInside(file.Path, known, comparison)));

        foreach (IGrouping<string, ChangedFile> group in fresh.GroupBy(file => Path.GetDirectoryName(file.Path) ?? ""))
        {
            SearchRoot root = group.First().Root;
            string folder = Raise(group.Key, root, tokens, comparison);

            raised.Add(new Proposal(folder, root, group.ToList()));
        }

        var result = new List<Proposal>();

        foreach (Proposal proposal in raised.OrderBy(p => p.Folder.Length))
        {
            // Una cartella del gioco dentro una che non lo è resta a parte (vedi SaveFinder.Roots).
            int outer = result.FindIndex(kept =>
                string.Equals(kept.Folder, proposal.Folder, comparison)
                || (IsInside(proposal.Folder, kept.Folder, comparison)
                    && (!proposal.Root.GameOwned || kept.Root.GameOwned)));

            if (outer >= 0)
                result[outer].Files.AddRange(proposal.Files);
            else
                result.Add(proposal);
        }

        return result;
    }

    /// <summary>
    /// Sale dalla cartella madre verso la radice e prende la più alta il cui nome
    /// contiene una parola del gioco; senza, resta la cartella madre.
    /// </summary>
    private static string Raise(string parent, SearchRoot root, IReadOnlyList<string> tokens,
        StringComparison comparison)
    {
        string best = parent;

        for (string current = parent; IsInside(current, root.Folder, comparison);
             current = Path.GetDirectoryName(current) ?? root.Folder)
        {
            if (MatchesToken(Path.GetFileName(current), tokens))
                best = current;
        }

        return best;
    }

    /// <summary>
    /// Punteggio di una cartella proposta, con i motivi (vedi Rank).
    /// </summary>
    private static SaveCandidate Score(Proposal proposal, IReadOnlyList<string> tokens, StringComparison comparison)
    {
        var reasons = new List<ScoreReason>();
        string named = NamedFolder(proposal.Folder, proposal.Root.Folder, tokens, comparison);

        if (named != "")
            reasons.Add(new ScoreReason(ScoreReasonKind.GameName, GameNamePoints, named));

        if (proposal.Root.GameOwned)
            reasons.Add(new ScoreReason(ScoreReasonKind.GameOwned, GameOwnedPoints, ""));

        string saveFile = proposal.Files.Select(file => Path.GetFileName(file.Path)).FirstOrDefault(IsSaveLike) ?? "";

        if (saveFile != "")
            reasons.Add(new ScoreReason(ScoreReasonKind.SaveLikeFile, SaveLikeFilePoints, saveFile));

        int changed = Math.Min(proposal.Files.Count * ChangedFilePoints, ChangedFilesMaxPoints);
        reasons.Add(new ScoreReason(ScoreReasonKind.ChangedFiles, changed,
            proposal.Files.Count.ToString(CultureInfo.InvariantCulture)));

        List<string> words = proposal.Files
            .SelectMany(file => FoldersBetween(file.Path, proposal.Root.Folder, comparison))
            .Where(name => SaveFolderWords.Contains(Normalize(name)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (words.Count > 0)
        {
            int points = words.Count > 1 ? SaveWordsPoints : SaveWordPoints;
            reasons.Add(new ScoreReason(ScoreReasonKind.SaveLikePath, points, string.Join(", ", words)));
        }

        if (proposal.Files.All(file => IsSettingsLike(file.Path)))
        {
            reasons.Add(new ScoreReason(ScoreReasonKind.OnlySettings, SettingsOnlyPoints,
                proposal.Files.Count.ToString(CultureInfo.InvariantCulture)));
        }

        if (!proposal.Root.GameOwned && named == "")
            reasons.Add(new ScoreReason(ScoreReasonKind.OtherProgram, OtherProgramPoints, ""));

        return new SaveCandidate(proposal.Folder, reasons.Sum(reason => reason.Points), proposal.Files, reasons);
    }

    /// <summary>
    /// Nome della prima cartella, dalla proposta verso la radice (esclusa), che
    /// contiene una parola del gioco; "" se nessuna.
    /// </summary>
    private static string NamedFolder(string folder, string root, IReadOnlyList<string> tokens,
        StringComparison comparison)
    {
        for (string current = folder; IsInside(current, root, comparison);
             current = Path.GetDirectoryName(current) ?? root)
        {
            string name = Path.GetFileName(current);

            if (MatchesToken(name, tokens))
                return name;
        }

        return "";
    }

    /// <summary>
    /// Nomi delle cartelle tra il file e la radice (esclusa): dalla cartella madre in su.
    /// </summary>
    private static IEnumerable<string> FoldersBetween(string file, string root, StringComparison comparison)
    {
        for (string current = Path.GetDirectoryName(file) ?? ""; IsInside(current, root, comparison);
             current = Path.GetDirectoryName(current) ?? root)
        {
            yield return Path.GetFileName(current);
        }
    }

    /// <summary>
    /// true se il nome ha un'estensione o una parola da salvataggio.
    /// </summary>
    private static bool IsSaveLike(string name)
    {
        string lower = name.ToLowerInvariant();

        return SaveExtensions.Contains(Path.GetExtension(lower))
            || SaveWords.Any(word => lower.Contains(word, StringComparison.Ordinal));
    }

    /// <summary>
    /// true se il file è un'impostazione: estensione da configurazione e nome che lo dice.
    /// </summary>
    private static bool IsSettingsLike(string file)
    {
        string name = Path.GetFileName(file).ToLowerInvariant();

        return SettingsExtensions.Contains(Path.GetExtension(name))
            && SettingsWords.Any(word => name.Contains(word, StringComparison.Ordinal));
    }

    /// <summary>
    /// File cambiati dopo sinceUtc nelle cartelle date, senza entrare nelle
    /// cartelle da saltare. Si ferma dopo timeLimit o se cancel è richiesto:
    /// partial dice che la lettura non è completa. Non scrive nulla.
    /// </summary>
    public static IReadOnlyList<ChangedFile> FindChanged(IReadOnlyList<SearchRoot> roots, DateTime sinceUtc,
        IReadOnlyList<string> skipFolders, TimeSpan timeLimit, CancellationToken cancel, out bool partial) =>
        Scan(roots, sinceUtc, skipFolders, timeLimit, cancel, out partial, null);

    /// <summary>
    /// Come FindChanged; visited, se non è null, riceve le cartelle lette (per i test).
    /// </summary>
    internal static IReadOnlyList<ChangedFile> Scan(IReadOnlyList<SearchRoot> roots, DateTime sinceUtc,
        IReadOnlyList<string> skipFolders, TimeSpan timeLimit, CancellationToken cancel, out bool partial,
        List<string>? visited)
    {
        var walker = new Walker(sinceUtc, skipFolders, timeLimit, cancel, visited);

        foreach (SearchRoot root in roots)
        {
            if (!walker.Walk(root.Folder, root, 0))
                break;
        }

        partial = walker.Stopped;
        return walker.Found;
    }

    // Cartelle che non tengono salvataggi (cache, programmi, log...): non si leggono.
    private static readonly HashSet<string> SkipNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Temp", "tmp", "Cache", "Caches", ".cache", "cache2", "GPUCache", "Code Cache", "ShaderCache",
        "shadercache", "D3DSCache", "DXCache", "GLCache", "NVIDIA", "NVIDIA Corporation", "AMD", "Intel",
        "Microsoft", "Packages", "Google", "Mozilla", "BraveSoftware", "discord", "Spotify", "CrashDumps",
        "Crashes", "CrashReports", "crashpad", "logs", "htmlcache", "webcache", "mesa_shader_cache",
        "fontconfig", "pulse", "Trash", ".Trash-1000", "node_modules", ".git"
    };

    // Cartelle del sistema che si saltano solo direttamente sotto la radice di un disco.
    private static readonly HashSet<string> DriveRootNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "windows", "Program Files", "Program Files (x86)"
    };

    // Estensioni di file che non sono salvataggi (log, temporanei, dump, lock).
    private static readonly HashSet<string> IgnoredExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".log", ".tmp", ".dmp", ".etl", ".lock", ".pid", ".pf"
    };

    /// <summary>
    /// Percorre le cartelle di ricerca e raccoglie i file cambiati. Ogni file
    /// compare una volta sola (vince la prima radice in cui è trovato).
    /// </summary>
    private sealed class Walker(DateTime sinceUtc, IReadOnlyList<string> skipFolders, TimeSpan limit,
        CancellationToken cancel, List<string>? visited)
    {
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly HashSet<string> seen = new(OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal);

        public List<ChangedFile> Found { get; } = [];

        /// <summary>true se la lettura si è fermata prima della fine.</summary>
        public bool Stopped { get; private set; }

        /// <summary>
        /// Legge una cartella e le sottocartelle fino alla profondità della radice.
        /// false se bisogna fermarsi del tutto.
        /// </summary>
        public bool Walk(string folder, SearchRoot root, int level)
        {
            if (IsSkipped(folder, root))
                return true;

            if (!KeepGoing())
                return false;

            visited?.Add(folder);

            string[] entries;

            try
            {
                entries = Directory.GetFileSystemEntries(folder);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Write("Cartella non letta durante la ricerca: " + folder, ex);
                return true;
            }

            foreach (string entry in entries)
            {
                if (!KeepGoing())
                    return false;

                FileAttributes attributes;

                try
                {
                    attributes = File.GetAttributes(entry);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Log.Write("Elemento non letto durante la ricerca: " + entry, ex);
                    continue;
                }

                // Mai seguire i collegamenti simbolici né le giunzioni.
                if (attributes.HasFlag(FileAttributes.ReparsePoint))
                    continue;

                if (attributes.HasFlag(FileAttributes.Directory))
                {
                    if (level < root.MaxDepth && !Walk(entry, root, level + 1))
                        return false;
                }
                else
                {
                    Consider(entry, root);
                }
            }

            return true;
        }

        /// <summary>
        /// Aggiunge il file se è cambiato dopo sinceUtc e non è un file da ignorare.
        /// </summary>
        private void Consider(string file, SearchRoot root)
        {
            string name = Path.GetFileName(file);

            if (name.EndsWith('~') || IgnoredExtensions.Contains(Path.GetExtension(name)))
                return;

            try
            {
                var info = new FileInfo(file);
                DateTime changed = info.LastWriteTimeUtc > info.CreationTimeUtc ? info.LastWriteTimeUtc : info.CreationTimeUtc;

                if (changed < sinceUtc || !seen.Add(file))
                    return;

                Found.Add(new ChangedFile(file, info.Length, info.LastWriteTimeUtc, root));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Write("File non letto durante la ricerca: " + file, ex);
            }
        }

        /// <summary>
        /// true se la cartella non si legge: nome di sistema, radice di un disco
        /// per i nomi di Windows, o dentro una cartella da saltare. Una cartella da
        /// saltare che contiene la radice stessa non conta: sul Deck il prefisso di
        /// Proton, l'installazione e la userdata del gioco stanno dentro la cartella
        /// di Steam, e vanno letti.
        /// </summary>
        private bool IsSkipped(string folder, SearchRoot root)
        {
            string name = Path.GetFileName(folder);

            if (SkipNames.Contains(name))
                return true;

            if (DriveRootNames.Contains(name) && Path.GetDirectoryName(folder) == Path.GetPathRoot(folder))
                return true;

            StringComparison comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            return skipFolders
                .Where(skip => !string.Equals(root.Folder, skip, comparison) && !IsInside(root.Folder, skip, comparison))
                .Any(skip => string.Equals(folder, skip, comparison) || IsInside(folder, skip, comparison));
        }

        /// <summary>
        /// false (e segna Stopped) se la richiesta è annullata o il tempo è finito.
        /// </summary>
        private bool KeepGoing()
        {
            if (cancel.IsCancellationRequested || clock.Elapsed > limit)
                Stopped = true;

            return !Stopped;
        }
    }

    /// <summary>
    /// Cartelle figlie di una cartella, o nessuna se non si legge.
    /// </summary>
    private static IEnumerable<string> Children(string folder)
    {
        if (folder == "" || !Directory.Exists(folder))
            return [];

        try
        {
            return Directory.EnumerateDirectories(folder).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Write("Cartella non letta: " + folder, ex);
            return [];
        }
    }
}
