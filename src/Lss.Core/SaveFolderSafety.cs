namespace Lss;

/// <summary>Perché una cartella non si può usare come cartella dei salvataggi.</summary>
internal enum SafetyProblem
{
    /// <summary>Nessun problema: la cartella si può sostituere per intero.</summary>
    None,

    /// <summary>La cartella non esiste.</summary>
    Missing,

    /// <summary>La cartella non ha file a nessun livello.</summary>
    Empty,

    /// <summary>È una cartella di sistema, di un launcher o del gioco.</summary>
    ProtectedFolder,

    /// <summary>Troppi file o troppi byte per essere una cartella dei salvataggi.</summary>
    TooBig,

    /// <summary>È dentro i dati di LSS.</summary>
    InsideLssData,

    /// <summary>Non si riesce a leggere il contenuto (permessi, errori di disco).</summary>
    Unreadable
}

/// <summary>
/// Controllo di sicurezza prima di sostituire una cartella dei salvataggi:
/// FileOps.ReplaceFolder la rende identica alla partita, quindi una cartella
/// sbagliata (Documents intero, la cartella del gioco) verrebbe distrutta.
/// Ogni cartella passa da qui, quando si trova e quando si apre il gioco.
/// </summary>
internal static class SaveFolderSafety
{
    public const long MaxBytes = 2L * 1024 * 1024 * 1024; // 2 GB
    public const int MaxFiles = 20_000;

    /// <summary>Se la cartella si può sostituire per intero cambiando partita.</summary>
    public static SafetyProblem Check(string folder, SaveContext context,
        IReadOnlyList<string> launcherFolders, string lssDataRoot) =>
        Check(folder, context, launcherFolders, lssDataRoot, MaxBytes, MaxFiles);

    /// <summary>
    /// Come Check, con i limiti dati (i test usano limiti piccoli).
    /// </summary>
    internal static SafetyProblem Check(string folder, SaveContext context,
        IReadOnlyList<string> launcherFolders, string lssDataRoot, long maxBytes, int maxFiles)
    {
        if (!Directory.Exists(folder))
            return SafetyProblem.Missing;

        // La radice di un disco (C:\, /) non si sostituisce mai.
        string absolute = Path.GetFullPath(folder);

        if (absolute == Path.GetPathRoot(absolute))
            return SafetyProblem.ProtectedFolder;

        StringComparison comparison = CaseInsensitive(context)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        string full = Normalize(folder);

        if (IsProtected(full, context, launcherFolders, comparison))
            return SafetyProblem.ProtectedFolder;

        if (lssDataRoot != "" && IsSameOrInside(full, Normalize(lssDataRoot), comparison))
            return SafetyProblem.InsideLssData;

        return CheckFiles(folder, maxBytes, maxFiles);
    }

    /// <summary>
    /// true se la cartella è uguale a una cartella protetta o ne è un genitore
    /// (un genitore contiene la cartella protetta: sostituirlo sarebbe peggio).
    /// </summary>
    private static bool IsProtected(string full, SaveContext context, IReadOnlyList<string> launcherFolders,
        StringComparison comparison)
    {
        return ProtectedPaths(context, launcherFolders)
            .Any(protectedPath => IsSameOrParent(full, protectedPath, comparison));
    }

    /// <summary>
    /// Le cartelle da proteggere: quelle dell'utente, di Windows e del prefisso,
    /// dei launcher, del gioco installato.
    /// </summary>
    private static IEnumerable<string> ProtectedPaths(SaveContext context, IReadOnlyList<string> launcherFolders)
    {
        KnownFolders folders = context.Folders;

        foreach (string value in new[]
                 {
                     folders.Home, folders.AppData, folders.LocalAppData, folders.LocalAppDataLow,
                     folders.Documents, folders.SavedGames, folders.Public, folders.ProgramData,
                     folders.XdgData, folders.XdgConfig
                 })
        {
            if (value != "")
                yield return Normalize(value);
        }

        if (context.PrefixPath != "")
        {
            string prefix = Normalize(context.PrefixPath);
            string drive = Path.Combine(prefix, "drive_c");

            yield return prefix;
            yield return drive;
            yield return Path.Combine(drive, "users");

            string user = SavePathResolver.PrefixUser(context);

            if (user != "")
            {
                yield return Normalize(user);

                foreach (string inside in new[]
                         {
                             Path.Combine("AppData", "Roaming"), Path.Combine("AppData", "Local"),
                             Path.Combine("AppData", "LocalLow"), "Documents", "My Documents", "Saved Games"
                         })
                {
                    yield return Normalize(Path.Combine(user, inside));
                }
            }
        }

        foreach (string launcher in launcherFolders)
            yield return Normalize(launcher);

        if (context.InstallPath != "")
            yield return Normalize(context.InstallPath);
    }

    /// <summary>
    /// true se candidate è uguale a protectedPath o ne è un genitore.
    /// </summary>
    private static bool IsSameOrParent(string candidate, string protectedPath, StringComparison comparison) =>
        string.Equals(candidate, protectedPath, comparison)
        || protectedPath.StartsWith(WithSeparator(candidate), comparison);

    /// <summary>
    /// true se candidate è uguale a dir o sta dentro.
    /// </summary>
    private static bool IsSameOrInside(string candidate, string dir, StringComparison comparison) =>
        string.Equals(candidate, dir, comparison)
        || candidate.StartsWith(WithSeparator(dir), comparison);

    /// <summary>
    /// Il percorso con il separatore finale (per la radice, "" diventa "/" o "\").
    /// </summary>
    private static string WithSeparator(string path)
    {
        char separator = Path.DirectorySeparatorChar;

        return path.EndsWith(separator) ? path : path + separator;
    }

    /// <summary>
    /// Percorso assoluto senza separatore finale, con il collegamento risolto
    /// se la cartella esiste. La radice di Linux diventa "".
    /// </summary>
    private static string Normalize(string path)
    {
        string full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        try
        {
            if (Directory.Exists(full) && new DirectoryInfo(full).ResolveLinkTarget(returnFinalTarget: true) is { } target)
                return Path.GetFullPath(target.FullName).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Write("Collegamento non risolto: " + path, ex);
        }

        return full;
    }

    /// <summary>
    /// Windows non distingue maiuscole e minuscole, e nel prefisso i nomi
    /// possono cambiare caso: in quei casi il confronto ignora le maiuscole.
    /// </summary>
    private static bool CaseInsensitive(SaveContext context) =>
        context.IgnoreCase || OperatingSystem.IsWindows();

    /// <summary>
    /// Conta i file a qualsiasi livello; si ferma appena supera i limiti.
    /// </summary>
    private static SafetyProblem CheckFiles(string folder, long maxBytes, int maxFiles)
    {
        int count = 0;
        long bytes = 0;

        try
        {
            foreach (string file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            {
                count++;
                bytes += new FileInfo(file).Length;

                if (count > maxFiles || bytes > maxBytes)
                    return SafetyProblem.TooBig;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Write("Cartella dei salvataggi non leggibile: " + folder, ex);
            return SafetyProblem.Unreadable;
        }

        return count == 0 ? SafetyProblem.Empty : SafetyProblem.None;
    }
}
