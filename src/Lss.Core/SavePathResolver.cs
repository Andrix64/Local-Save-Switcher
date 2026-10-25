using System.Globalization;

namespace Lss;

/// <summary>Dati per trasformare le regole in cartelle.</summary>
/// <param name="Folders">Cartelle dell'utente di questo sistema.</param>
/// <param name="PrefixPath">Prefisso Proton o Wine del gioco, o "".</param>
/// <param name="PrefixIsPerGame">true se il prefisso è di un solo gioco.</param>
/// <param name="InstallPath">Cartella del gioco installato, o "".</param>
/// <param name="AccountId">Numero dell'account (32 bit per Steam), o "".</param>
/// <param name="IgnoreCase">true se i nomi nel prefisso vanno confrontati senza maiuscole.</param>
public sealed record SaveContext(
    KnownFolders Folders,
    string PrefixPath,
    bool PrefixIsPerGame,
    string InstallPath,
    string AccountId,
    bool IgnoreCase);

/// <summary>
/// Trasforma una regola di un launcher in cartelle e file del disco: radici
/// (Documents, AppData...) dentro il prefisso o sul sistema, sostituzioni
/// rootoverrides, token come {64BitSteamID}, maschere dei nomi.
/// </summary>
internal static class SavePathResolver
{
    // Base degli ID Steam a 64 bit: ID a 64 bit = base + numero dell'account.
    private const ulong SteamId64Base = 76561197960265728;

    // Radici conosciute su Windows e nel prefisso: nome → cartelle da provare, in ordine.
    private static readonly Dictionary<string, string[][]> PrefixFolders = new()
    {
        ["WinMyDocuments"] = [["Documents"], ["My Documents"]],
        ["WinAppDataRoaming"] = [["AppData", "Roaming"]],
        ["WinAppDataLocal"] = [["AppData", "Local"]],
        ["WinAppDataLocalLow"] = [["AppData", "LocalLow"]],
        ["WinSavedGames"] = [["Saved Games"]],
        ["WinUserProfile"] = [[]],
    };

    private static readonly HashSet<string> KnownRoots =
    [
        "gameinstall", "WinMyDocuments", "WinAppDataRoaming", "WinAppDataLocal", "WinAppDataLocalLow",
        "WinSavedGames", "WinUserProfile", "LinuxHome", "LinuxXdgDataHome", "LinuxXdgConfigHome"
    ];

    /// <summary>
    /// "Windows" se il gioco ha un prefisso o siamo su Windows, altrimenti "Linux".
    /// </summary>
    public static string PlatformFor(SaveContext context) =>
        context.PrefixPath != "" || OperatingSystem.IsWindows() ? "Windows" : "Linux";

    /// <summary>
    /// Cartella dell'utente dentro il prefisso (drive_c/users/steamuser, o quella
    /// con il nome dell'utente Linux per Wine), o "" se non c'è.
    /// </summary>
    public static string PrefixUser(SaveContext context)
    {
        if (context.PrefixPath == "")
            return "";

        foreach (string name in new[] { "steamuser", context.Folders.UserName })
        {
            if (name == "")
                continue;

            string found = FoldersNamed(context.PrefixPath, ["drive_c", "users", name], CaseInsensitive(context))
                .FirstOrDefault() ?? "";

            if (found != "")
                return found;
        }

        return "";
    }

    /// <summary>
    /// Cartella di una radice, o "" se la radice non ha valore qui (per
    /// esempio Documents su Linux senza prefisso, o una radice Mac).
    /// </summary>
    public static string RootFolder(string root, SaveContext context)
    {
        KnownFolders folders = context.Folders;
        bool linux = PlatformFor(context) == "Linux";

        if (root == "gameinstall")
            return context.InstallPath;

        if (root is "LinuxHome" or "LinuxXdgDataHome" or "LinuxXdgConfigHome")
        {
            if (!linux)
                return "";

            return root switch
            {
                "LinuxHome" => folders.Home,
                "LinuxXdgDataHome" => folders.XdgData,
                _ => folders.XdgConfig,
            };
        }

        if (context.PrefixPath != "")
            return PrefixRoot(root, context);

        // Senza prefisso su Linux le radici di Windows non esistono (lo dice
        // rootoverrides, se c'è).
        if (linux)
            return "";

        return root switch
        {
            "WinMyDocuments" => folders.Documents,
            "WinAppDataRoaming" => folders.AppData,
            "WinAppDataLocal" => folders.LocalAppData,
            "WinAppDataLocalLow" => folders.LocalAppDataLow,
            "WinSavedGames" => folders.SavedGames,
            "WinUserProfile" => folders.Home,
            _ => "",
        };
    }

    /// <summary>
    /// Percorsi che esistono per la regola: cartelle (pattern "*" ricorsivo) o
    /// file che corrispondono. Vuoto se la regola non vale su questo sistema,
    /// se la radice non esiste o se il percorso non è valido.
    /// </summary>
    public static IReadOnlyList<string> Resolve(SaveRule rule, IReadOnlyList<RootOverride> overrides,
        SaveContext context)
    {
        if (!AppliesTo(rule, context))
            return [];

        (string root, string path) = ApplyOverride(rule, overrides, context);

        if (!KnownRoots.Contains(root))
        {
            if (!root.StartsWith("Mac", StringComparison.Ordinal))
                Log.Write("Radice dei salvataggi non supportata: " + root);

            return [];
        }

        string folder = RootFolder(root, context);

        if (folder == "" || !Directory.Exists(folder))
            return [];

        string? resolved = ReplaceTokens(path, context);

        if (resolved == null)
        {
            Log.Write("Percorso dei salvataggi con un token sconosciuto: " + path);
            return [];
        }

        string[] segments = resolved.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (Path.IsPathRooted(resolved) || segments.Any(segment => segment == ".."))
        {
            Log.Write("Percorso dei salvataggi non valido: " + path);
            return [];
        }

        try
        {
            var results = new List<string>();
            string pattern = rule.Pattern == "*.*" ? "*" : rule.Pattern;

            foreach (string match in FoldersNamed(folder, segments.Where(s => s != ".").ToArray(), CaseInsensitive(context)))
            {
                if (pattern == "*" && rule.Recursive)
                    results.Add(match);
                else
                    results.AddRange(FilesOf(match, pattern, rule.Recursive, CaseInsensitive(context)));
            }

            return results
                .Select(Path.GetFullPath)
                .Distinct()
                .OrderBy(result => result, StringComparer.Ordinal)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Write("Regola dei salvataggi " + rule.Root + " non letta", ex);
            return [];
        }
    }

    /// <summary>
    /// Radice e percorso della regola, dopo la sostituzione di rootoverrides su
    /// Linux senza prefisso. Solo confronto "=" con os Linux; gli altri
    /// confronti si saltano e si registrano.
    /// </summary>
    private static (string Root, string Path) ApplyOverride(SaveRule rule, IReadOnlyList<RootOverride> overrides,
        SaveContext context)
    {
        if (PlatformFor(context) != "Linux")
            return (rule.Root, rule.Path);

        foreach (RootOverride over in overrides)
        {
            if (over.Root != rule.Root || !over.Os.Equals("Linux", StringComparison.OrdinalIgnoreCase))
                continue;

            if (over.OsCompare != "=")
            {
                Log.Write("Sostituzione di radice con confronto " + over.OsCompare + " saltata: " + rule.Root);
                continue;
            }

            string path = over.AddPath == "" ? rule.Path : JoinPath(over.AddPath, rule.Path);

            foreach ((string find, string replace) in over.Transforms)
                path = path.Replace(find, replace, StringComparison.Ordinal);

            return (over.UseInstead, path);
        }

        return (rule.Root, rule.Path);
    }

    private static string JoinPath(string first, string second) =>
        second == "" ? first : first + "/" + second;

    /// <summary>
    /// Sostituisce {64BitSteamID} e {Steam3AccountID}. null se il percorso ha
    /// altri token o un numero di account che non si può usare.
    /// </summary>
    private static string? ReplaceTokens(string path, SaveContext context)
    {
        string result = path;

        if (result.Contains("{64BitSteamID}", StringComparison.Ordinal))
        {
            if (!ulong.TryParse(context.AccountId, NumberStyles.None, CultureInfo.InvariantCulture, out ulong account))
                return null;

            string id64 = (SteamId64Base + account).ToString(CultureInfo.InvariantCulture);
            result = result.Replace("{64BitSteamID}", id64, StringComparison.Ordinal);
        }

        if (result.Contains("{Steam3AccountID}", StringComparison.Ordinal))
        {
            if (context.AccountId == "")
                return null;

            result = result.Replace("{Steam3AccountID}", context.AccountId, StringComparison.Ordinal);
        }

        return result.Contains('{') || result.Contains('}') ? null : result;
    }

    /// <summary>
    /// Radice dentro il prefisso: le cartelle di Windows stanno sotto
    /// drive_c/users/&lt;utente&gt;. La prima che esiste vince.
    /// </summary>
    private static string PrefixRoot(string root, SaveContext context)
    {
        if (!PrefixFolders.TryGetValue(root, out string[][]? options))
            return "";

        string user = PrefixUser(context);

        if (user == "")
            return "";

        foreach (string[] segments in options)
        {
            string found = FoldersNamed(user, segments, CaseInsensitive(context)).FirstOrDefault() ?? "";

            if (found != "")
                return found;
        }

        return "";
    }

    /// <summary>
    /// Cartelle che corrispondono ai nomi dati, uno dopo l'altro, partendo da
    /// start. Un nome senza maschere e senza ignoreCase si cerca direttamente;
    /// gli altri elencando la cartella.
    /// </summary>
    internal static IReadOnlyList<string> FoldersNamed(string start, IReadOnlyList<string> segments, bool ignoreCase)
    {
        List<string> current = [start];

        foreach (string segment in segments)
        {
            var next = new List<string>();

            foreach (string folder in current)
            {
                try
                {
                    if (!Glob.HasWildcard(segment) && !ignoreCase)
                    {
                        string child = Path.Combine(folder, segment);

                        if (Directory.Exists(child))
                            next.Add(child);
                    }
                    else
                    {
                        next.AddRange(Directory.EnumerateDirectories(folder)
                            .Where(child => Glob.IsMatch(segment, Path.GetFileName(child), ignoreCase)));
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Log.Write("Cartella dei salvataggi non letta: " + folder, ex);
                }
            }

            current = next;
        }

        return current;
    }

    /// <summary>
    /// File della cartella che corrispondono alla maschera; con recursive anche
    /// nelle sottocartelle.
    /// </summary>
    private static IReadOnlyList<string> FilesOf(string folder, string pattern, bool recursive, bool ignoreCase)
    {
        try
        {
            SearchOption option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            return Directory.EnumerateFiles(folder, "*", option)
                .Where(file => Glob.IsMatch(pattern, Path.GetFileName(file), ignoreCase))
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Write("Cartella dei salvataggi non letta: " + folder, ex);
            return [];
        }
    }

    private static bool AppliesTo(SaveRule rule, SaveContext context)
    {
        if (rule.Platforms.Count == 0)
            return true;

        string platform = PlatformFor(context);

        return rule.Platforms.Any(p =>
            p.Equals("all", StringComparison.OrdinalIgnoreCase) || p.Equals(platform, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Windows non distingue maiuscole e minuscole: il confronto è senza
    /// maiuscole anche se il contesto non lo chiede.
    /// </summary>
    private static bool CaseInsensitive(SaveContext context) => context.IgnoreCase || OperatingSystem.IsWindows();
}
