using System.Globalization;
using System.Text.RegularExpressions;

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
    /// Percorso portabile di una cartella: la radice tra graffe ({WinAppDataLocal}/Discord)
    /// se la cartella sta sotto una radice di questo sistema, altrimenti il percorso intero.
    /// Vale per ogni gioco, anche in un altro prefisso. Gli ID Steam dell'account
    /// diventano {64BitSteamID} e {Steam3AccountID}, così non finiscono nei file da condividere.
    /// </summary>
    internal static string ToPortable(string folder, SaveContext context) =>
        ReplaceAccountIds(PortableRoot(folder, context), context);

    private static string PortableRoot(string folder, SaveContext context)
    {
        string bestRoot = "";
        string bestFolder = "";

        foreach (string root in KnownRoots)
        {
            string rootFolder = RootFolder(root, context);

            if (rootFolder == "" || rootFolder.Length <= bestFolder.Length)
                continue;

            if (IsSameOrInside(folder, rootFolder, context))
            {
                bestRoot = root;
                bestFolder = rootFolder;
            }
        }

        if (bestRoot == "")
            return folder;

        string rest = folder[bestFolder.Length..].TrimStart('/', '\\');

        return rest == "" ? "{" + bestRoot + "}" : "{" + bestRoot + "}/" + rest.Replace('\\', '/');
    }

    /// <summary>
    /// Percorso di un percorso portabile in questo sistema e per questo gioco, o
    /// null se la radice non esiste qui (per esempio Documents su Linux senza prefisso).
    /// </summary>
    internal static string? FromPortable(string portable, SaveContext context)
    {
        if (!portable.StartsWith('{'))
            return ReplaceAccountTokens(portable, context);

        int close = portable.IndexOf('}');

        if (close < 0)
            return null;

        string folder = RootFolder(portable[1..close], context);

        if (folder == "")
            return null;

        string rest = ReplaceAccountTokens(portable[(close + 1)..].TrimStart('/', '\\'), context)
            .Replace('/', Path.DirectorySeparatorChar);

        return rest == "" ? folder : Path.Combine(folder, rest);
    }

    /// <summary>
    /// Sostituisce {64BitSteamID} e {Steam3AccountID} con l'ID dell'account di
    /// questo sistema. Gli altri testi restano com'erano.
    /// </summary>
    private static string ReplaceAccountTokens(string path, SaveContext context)
    {
        if (context.AccountId == "")
            return path;

        if (ulong.TryParse(context.AccountId, NumberStyles.None, CultureInfo.InvariantCulture, out ulong account))
            path = path.Replace("{64BitSteamID}", (SteamId64Base + account).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

        return path.Replace("{Steam3AccountID}", context.AccountId, StringComparison.Ordinal);
    }

    /// <summary>
    /// Sostituisce con i token l'ID a 64 bit e il numero di account quando sono
    /// un intero componente del percorso (non una parte di un nome).
    /// </summary>
    private static string ReplaceAccountIds(string path, SaveContext context)
    {
        if (context.AccountId == "" || !ulong.TryParse(context.AccountId, NumberStyles.None, CultureInfo.InvariantCulture, out ulong account))
            return path;

        string id64 = (SteamId64Base + account).ToString(CultureInfo.InvariantCulture);

        return Regex.Replace(path, @"(?<=^|[\\/])(" + id64 + "|" + Regex.Escape(context.AccountId) + @")(?=$|[\\/])",
            match => match.Value == id64 ? "{64BitSteamID}" : "{Steam3AccountID}");
    }

    private static bool IsSameOrInside(string folder, string root, SaveContext context)
    {
        StringComparison comparison = CaseInsensitive(context) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        string trimmed = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return string.Equals(folder, trimmed, comparison)
            || folder.StartsWith(trimmed + Path.DirectorySeparatorChar, comparison);
    }

    /// <summary>
    /// Percorsi che esistono per la regola: cartelle (pattern "*" ricorsivo) o
    /// file che corrispondono. Vuoto se la regola non vale su questo sistema,
    /// se la radice non esiste o se il percorso non è valido.
    /// </summary>
    public static IReadOnlyList<string> Resolve(SaveRule rule, IReadOnlyList<RootOverride> overrides,
        SaveContext context)
    {
        IReadOnlyList<string> folders = RuleFolders(rule, overrides, context);

        try
        {
            var results = new List<string>();
            string pattern = rule.Pattern == "*.*" ? "*" : rule.Pattern;

            foreach (string match in folders)
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
    /// Posti che la regola trova su questo sistema, senza ancora id: una cartella
    /// intera (maschera "*" o "*.*" ricorsiva) o, per ogni cartella trovata, un
    /// posto con la maschera (solo se ci sono file che corrispondono). Una maschera
    /// "*" senza ricorsione è troppo larga: TooWide, e il gioco non si può gestire.
    /// </summary>
    internal static (IReadOnlyList<SaveSpot> Spots, bool TooWide) ResolveSpots(SaveRule rule,
        IReadOnlyList<RootOverride> overrides, SaveContext context)
    {
        string pattern = rule.Pattern == "*.*" ? "*" : rule.Pattern;
        var spots = new List<SaveSpot>();
        bool tooWide = false;

        foreach (string folder in RuleFolders(rule, overrides, context))
        {
            string full = Path.GetFullPath(folder);

            if (pattern == "*")
            {
                if (rule.Recursive)
                    spots.Add(new SaveSpot(0, full, "", true, ToPortable(full, context), SaveSource.Launcher));
                else
                    tooWide = true;

                continue;
            }

            if (FilesOf(full, pattern, rule.Recursive, CaseInsensitive(context)).Count > 0)
                spots.Add(new SaveSpot(0, full, pattern, rule.Recursive, ToPortable(full, context), SaveSource.Launcher));
        }

        return (spots, tooWide);
    }

    /// <summary>
    /// Cartelle che la regola trova: la cartella di base (radice, percorso, token,
    /// nomi con maschera) che esiste su questo sistema. Vuoto se la regola non vale qui.
    /// </summary>
    private static IReadOnlyList<string> RuleFolders(SaveRule rule, IReadOnlyList<RootOverride> overrides,
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

        // Steam scrive a volte "/Cartella", "/" o "'.'": sono comunque relativi alla radice.
        string relative = resolved.Replace('\\', '/').TrimStart('/');
        string[] segments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(segment => segment != "." && segment != "'.'")
            .ToArray();

        if (Path.IsPathRooted(relative) || segments.Any(segment => segment == ".."))
        {
            Log.Write("Percorso dei salvataggi non valido: " + path);
            return [];
        }

        try
        {
            return FoldersNamed(folder, segments, CaseInsensitive(context));
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
