namespace Lss;

/// <summary>Cartella in cui cercare i file cambiati, con la profondità massima.</summary>
/// <param name="Folder">Cartella da leggere.</param>
/// <param name="MaxDepth">Livelli di sottocartelle da leggere sotto Folder.</param>
/// <param name="GameOwned">true se tutto quello che sta dentro appartiene a questo gioco.</param>
public sealed record SearchRoot(string Folder, int MaxDepth, bool GameOwned);

/// <summary>
/// Trova la cartella dei salvataggi di un gioco guardando i file cambiati
/// sul disco mentre il gioco era aperto (fase M). Legge soltanto.
/// </summary>
internal static partial class SaveFinder
{
    // Profondità per le cartelle che sono del gioco: installazione, userdata, prefisso.
    private const int GameDepth = 8;

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
