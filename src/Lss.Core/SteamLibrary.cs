namespace Lss;

/// <summary>
/// Legge le librerie, i manifest e i giochi di Steam senza modificare nulla.
/// </summary>
internal static class SteamLibrary
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    // Cartelle di userdata che non sono giochi.
    internal static readonly HashSet<string> SystemApps = ["7", "760", "241100"];

    // Nomi dei manifest che non sono giochi (runtime di Steam e di Proton).
    private static readonly string[] SystemNamePrefixes =
    [
        "Proton", "Steam Linux Runtime", "Steamworks Common Redistributables", "SteamVR"
    ];

    // Nomi noti dei giochi senza manifest (disinstallati ma con i salvataggi).
    private static readonly Dictionary<string, string> KnownNames = new()
    {
        [GameInfo.ControlAppId] = "Control"
    };

    /// <summary>
    /// Librerie di Steam da steamapps/libraryfolders.vdf, la cartella di Steam
    /// per prima. Solo le cartelle che esistono, senza doppioni.
    /// </summary>
    public static IReadOnlyList<string> LibraryFolders(string steamRoot)
    {
        var result = new List<string> { steamRoot };
        string file = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");

        if (!File.Exists(file))
            return result;

        try
        {
            Vdf.Node root = Vdf.Parse(File.ReadAllText(file));

            if (root.GetValueOrDefault("libraryfolders") is not Vdf.Node libraries)
                return result;

            foreach (object entry in libraries.Values)
            {
                if (entry is not Vdf.Node library || library.GetValueOrDefault("path") is not string path || path == "")
                    continue;

                if (Directory.Exists(path) && !Contains(result, path))
                    result.Add(path);
            }
        }
        catch (Exception ex)
        {
            Log.Write("Librerie di Steam non lette", ex);
        }

        return result;
    }

    /// <summary>
    /// Nome e cartella di installazione dal manifest del gioco, cercato in
    /// tutte le librerie. null se il gioco non è installato.
    /// </summary>
    public static (string Name, string InstallDir, string Library)? ReadManifest(
        IReadOnlyList<string> libraries, string appId)
    {
        foreach (string library in libraries)
        {
            string file = Path.Combine(library, "steamapps", "appmanifest_" + appId + ".acf");

            if (!File.Exists(file))
                continue;

            try
            {
                Vdf.Node root = Vdf.Parse(File.ReadAllText(file));
                string? name = Vdf.Find(root, "AppState", "name");

                if (!string.IsNullOrEmpty(name))
                    return (name, Vdf.Find(root, "AppState", "installdir") ?? "", library);
            }
            catch (Exception ex)
            {
                Log.Write("Manifest di Steam non letto: " + file, ex);
            }
        }

        return null;
    }

    /// <summary>
    /// Se LSS può gestire la cartella dei salvataggi di un gioco. Conta ogni
    /// file a qualsiasi profondità, tranne remotecache.vdf al primo livello
    /// (come FileOps.HasNoSaveFiles).
    /// </summary>
    public static SupportState GetSupport(string saveFolder)
    {
        try
        {
            if (!FileOps.HasNoSaveFiles(saveFolder))
                return SupportState.Supported;

            bool hasCache = File.Exists(Path.Combine(saveFolder, "remotecache.vdf"));
            return hasCache ? SupportState.SavesElsewhere : SupportState.NoSaves;
        }
        catch (Exception ex)
        {
            Log.Write("Cartella non leggibile: " + saveFolder, ex);
            return SupportState.NoSaves;
        }
    }

    /// <summary>
    /// Prefisso Proton del gioco (steamapps/compatdata/AppID/pfx), cercato prima
    /// nella libreria del gioco e poi in tutte le altre. "" su Windows o se non c'è.
    /// </summary>
    public static string FindProtonPrefix(string steamRoot, GameInfo game)
    {
        if (OperatingSystem.IsWindows())
            return "";

        var libraries = new List<string>();

        if (game.LibraryPath != "")
            libraries.Add(game.LibraryPath);

        libraries.AddRange(LibraryFolders(steamRoot));

        foreach (string library in libraries)
        {
            string prefix = Path.Combine(library, "steamapps", "compatdata", game.Id, "pfx");

            if (Directory.Exists(prefix))
                return prefix;
        }

        return "";
    }

    /// <summary>
    /// Icona (32×32) e copertina (300×450) del gioco dalla cache di Steam.
    /// "" se non ci sono.
    /// </summary>
    public static (string Icon, string Cover) FindImages(string steamRoot, string appId)
    {
        string cache = Path.Combine(steamRoot, "appcache", "librarycache");
        string folder = Path.Combine(cache, appId);

        try
        {
            string? icon = IconIn(folder) ?? FirstFile(Path.Combine(cache, appId + "_icon.jpg"));
            string? cover = FirstFile(
                Path.Combine(folder, "library_600x900.jpg"),
                Path.Combine(cache, appId + "_library_600x900.jpg"));

            return (icon ?? "", cover ?? "");
        }
        catch (Exception)
        {
            return ("", "");
        }
    }

    /// <summary>
    /// Icona della cartella: il primo .jpg il cui nome è un hash di 40 caratteri esadecimali.
    /// </summary>
    private static string? IconIn(string folder)
    {
        if (!Directory.Exists(folder))
            return null;

        return Directory.GetFiles(folder, "*.jpg")
            .Where(file => IsHash(Path.GetFileNameWithoutExtension(file)))
            .OrderBy(file => Path.GetFileName(file), StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static bool IsHash(string name) =>
        name.Length == 40 && name.All(char.IsAsciiHexDigitLower);

    private static string? FirstFile(params string[] files) =>
        files.FirstOrDefault(File.Exists);

    /// <summary>
    /// Giochi con una cartella in userdata/IDAccount, i supportati per primi,
    /// poi in ordine di nome.
    /// </summary>
    public static IReadOnlyList<GameInfo> FindGames(string steamRoot, string accountFolder)
    {
        var games = new List<GameInfo>();

        if (!Directory.Exists(accountFolder))
            return games;

        IReadOnlyList<string> libraries = LibraryFolders(steamRoot);

        foreach (string dir in Directory.GetDirectories(accountFolder))
        {
            string appId = Path.GetFileName(dir);

            if (!appId.All(char.IsAsciiDigit) || SystemApps.Contains(appId))
                continue;

            var manifest = ReadManifest(libraries, appId);

            if (manifest is { } found && IsSystemName(found.Name))
                continue;

            string name = manifest?.Name
                ?? KnownNames.GetValueOrDefault(appId)
                ?? Loc.F(Strings.UnknownGameName, appId);
            (string icon, string cover) = FindImages(steamRoot, appId);

            string installDir = manifest?.InstallDir ?? "";
            string library = manifest?.Library ?? "";
            string installPath = installDir != "" && library != ""
                ? Path.Combine(library, "steamapps", "common", installDir)
                : "";

            games.Add(new GameInfo(
                "steam",
                appId,
                name,
                installDir,
                library,
                installPath,
                dir,
                GetSupport(dir),
                icon,
                cover));
        }

        // Giochi installati senza cartella in userdata: il gioco salva altrove
        // (Proton, Documents...) e va trovato con le regole di Steam.
        var listed = games.Select(game => game.Id).ToHashSet();

        foreach ((string appId, string name, string installDir, string library) in InstalledApps(libraries))
        {
            if (listed.Contains(appId) || SystemApps.Contains(appId))
                continue;

            (string icon, string cover) = FindImages(steamRoot, appId);
            string installPath = installDir != "" && library != ""
                ? Path.Combine(library, "steamapps", "common", installDir)
                : "";

            games.Add(new GameInfo(
                "steam",
                appId,
                name,
                installDir,
                library,
                installPath,
                "",
                SupportState.NoSaves,
                icon,
                cover,
                SaveSource.None));
        }

        return games
            .OrderBy(game => game.Support != SupportState.Supported)
            .ThenBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Giochi installati in tutte le librerie, da steamapps/appmanifest_*.acf.
    /// Salta i manifest senza nome e quelli che non sono giochi (runtime, Proton).
    /// </summary>
    internal static IEnumerable<(string AppId, string Name, string InstallDir, string Library)> InstalledApps(
        IReadOnlyList<string> libraries)
    {
        var apps = new List<(string AppId, string Name, string InstallDir, string Library)>();

        foreach (string library in libraries)
        {
            string steamapps = Path.Combine(library, "steamapps");

            if (!Directory.Exists(steamapps))
                continue;

            foreach (string file in Directory.GetFiles(steamapps, "appmanifest_*.acf"))
            {
                string appId = Path.GetFileNameWithoutExtension(file)["appmanifest_".Length..];

                if (!appId.All(char.IsAsciiDigit))
                    continue;

                try
                {
                    Vdf.Node root = Vdf.Parse(File.ReadAllText(file));
                    string? name = Vdf.Find(root, "AppState", "name");

                    if (string.IsNullOrEmpty(name) || IsSystemName(name))
                        continue;

                    apps.Add((appId, name, Vdf.Find(root, "AppState", "installdir") ?? "", library));
                }
                catch (Exception ex)
                {
                    Log.Write("Manifest di Steam non letto: " + file, ex);
                }
            }
        }

        return apps;
    }

    private static bool IsSystemName(string name) =>
        SystemNamePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static bool Contains(List<string> paths, string path) =>
        paths.Any(existing => string.Equals(NormalizePath(existing), NormalizePath(path), PathComparison));

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
